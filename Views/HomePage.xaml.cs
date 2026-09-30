using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MovieBookRecommender.Models;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    public partial class HomePage : UserControl
    {
        private DataManager _data = null!;
        private MainWindow _app = null!;
        private MediaItem? _currentHeroItem;
        private List<MediaItem> _heroCandidates = new();
        private int _currentHeroIndex = 0;
        private readonly DispatcherTimer _searchDebounceTimer;
        private readonly DispatcherTimer _lostFocusTimer;
        private List<MediaItem> _recsItems = new();
        private List<MediaItem> _popularItems = new();
        private List<MediaItem> _newItems = new();
        private int _currentColumns = -1;

        private readonly List<(Border border, MediaItem item)> _dropdownItems = new();
        private int _selectedDropdownIndex = -1;

        public HomePage()
        {
            InitializeComponent();

            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(160)
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;

            _lostFocusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _lostFocusTimer.Tick += (_, _) =>
            {
                _lostFocusTimer.Stop();
                if (!SearchBox.IsKeyboardFocusWithin)
                    SearchPopup.IsOpen = false;
            };

            SearchPopup.Closed += (_, _) =>
            {
                _selectedDropdownIndex = -1;
                UpdateDropdownSelection();
            };
        }

        public void Initialize(DataManager data, MainWindow app)
        {
            _data = data;
            _app = app;

            if (Application.Current?.MainWindow != null)
            {
                var win = Application.Current.MainWindow;
                win.Deactivated += (_, _) =>
                {
                    if (SearchPopup != null)
                        SearchPopup.IsOpen = false;
                };

                win.PreviewMouseDown += (s, e) =>
                {
                    if (SearchPopup == null || !SearchPopup.IsOpen) return;

                    var boxPos = e.GetPosition(SearchBoxContainer);
                    bool inBox = boxPos.X >= 0 && boxPos.X <= SearchBoxContainer.ActualWidth &&
                                 boxPos.Y >= 0 && boxPos.Y <= SearchBoxContainer.ActualHeight;

                    bool inPopup = false;
                    if (SearchPopup.Child is FrameworkElement popupContent)
                    {
                        var popupPos = e.GetPosition(popupContent);
                        inPopup = popupPos.X >= 0 && popupPos.X <= popupContent.ActualWidth &&
                                  popupPos.Y >= 0 && popupPos.Y <= popupContent.ActualHeight;
                    }

                    if (!inBox && !inPopup)
                    {
                        SearchPopup.IsOpen = false;
                    }
                };
            }

            RefreshData();
        }

        public double GetScrollOffset() => MainScroll?.VerticalOffset ?? 0;

        public void RestoreScrollOffset(double offset)
        {
            if (offset <= 0)
            {
                MainScroll?.ScrollToTop();
                return;
            }

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                MainScroll?.ScrollToVerticalOffset(offset);
            }));
        }

        public void ScrollToTop()
        {
            MainScroll?.ScrollToTop();
        }

        public void UpdateCardsFavoriteState()
        {
            UpdateHeroFavButton();
            UpdateGridFavorites(RecsGrid);
            UpdateGridFavorites(PopularGrid);
            UpdateGridFavorites(NewGrid);
        }

        private static void UpdateGridFavorites(UniformGrid? grid)
        {
            if (grid == null) return;
            foreach (var child in grid.Children)
            {
                if (child is CardControl card)
                    card.UpdateFavIcon(animate: false);
            }
        }

        public void RefreshData()
        {
            _currentColumns = -1;
            BuildHeroCandidates();

            if (_currentHeroItem == null || !_heroCandidates.Any(i => i.Id == _currentHeroItem.Id))
            {
                _currentHeroIndex = 0;
                if (_heroCandidates.Count > 0)
                    ShowHeroItem(_heroCandidates[0], animate: false);
            }
            else
            {
                UpdateHeroFavButton();
            }

            var usedIds = new HashSet<string>();
            if (_currentHeroItem != null)
                usedIds.Add(_currentHeroItem.Id);

            var allItems = _data.AllItems();

            // 1. Секция «Рекомендуем для вас» (до 6 карточек для адаптивной ширины)
            _recsItems = RecommendationService.BuildPersonalizedFeed(allItems, _data.Favorites, limit: 6, excludeIds: usedIds);
            foreach (var r in _recsItems) usedIds.Add(r.Id);

            RecsSubtitle.Text = _data.Favorites.Count > 0
                ? "На основе ваших предпочтений"
                : "На основе рейтинга и популярности";

            if (_recsItems.Count == 0)
            {
                RecsEmptyBanner.Visibility = Visibility.Visible;
                RecsGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                RecsEmptyBanner.Visibility = Visibility.Collapsed;
                RecsGrid.Visibility = Visibility.Visible;
            }

            // 2. Секция «Популярное» (до 6 карточек)
            _popularItems = RecommendationService.BuildPopularFeed(allItems, excludeIds: usedIds, limit: 6);
            foreach (var p in _popularItems) usedIds.Add(p.Id);

            // 3. Секция «Новинки» (до 6 карточек)
            _newItems = RecommendationService.BuildNewReleasesFeed(allItems, excludeIds: usedIds, limit: 6);

            // Адаптивная ширина карточек
            ApplyResponsiveWidth(GetAvailableContentWidth());

            // Если поиск активен — обновляем результаты поиска
            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                PerformSearch(SearchBox.Text);
            }
        }

        // ------------------------------------------------------------------
        // Hero
        // ------------------------------------------------------------------
        private void BuildHeroCandidates()
        {
            _heroCandidates = RecommendationService.PickFeaturedCandidates(_data.AllItems(), _data.Favorites, count: 6);
            if (_currentHeroIndex >= _heroCandidates.Count)
                _currentHeroIndex = 0;
        }

        private void ShowHeroItem(MediaItem item, bool animate = true)
        {
            if (item is null) return;
            _currentHeroItem = item;

            if (!animate)
            {
                ApplyHeroData(item);
                return;
            }

            // Плавное переключение Hero (opacity 1 -> 0, затем 0 -> 1 за 220–250 ms)
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(110))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (_, _) =>
            {
                ApplyHeroData(item);
                var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(130))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                HeroFrame.BeginAnimation(OpacityProperty, fadeIn);
            };
            HeroFrame.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void ApplyHeroData(MediaItem item)
        {
            _currentHeroItem = item;

            HeroTitle.Text = item.Title;
            HeroGenresText.Text = item.GenreText;
            HeroRatingText.Text = $"{item.Rating:F1}";

            var role = item.Kind == MediaKind.Movie ? "Режиссёр" : "Автор";
            var metaExtra = !string.IsNullOrWhiteSpace(item.Author) ? $"{role}: {item.Author}" : "";
            if (item.Year > 0)
                metaExtra = string.IsNullOrEmpty(metaExtra) ? $"{item.Year} г." : $"{metaExtra}  •  {item.Year} г.";
            HeroMetaSubText.Text = metaExtra;

            HeroDescription.Text = item.Description;
            HeroPrimaryBtn.Content = item.Kind == MediaKind.Movie ? "▷  Смотреть" : "▷  Читать";
            UpdateHeroFavButton();

            var img = PosterService.GetImage(item);
            HeroPosterImage.Source = img;
            HeroBgImage.Source = img;
        }

        private void UpdateHeroFavButton()
        {
            if (_currentHeroItem is null) return;
            var isFav = _data.IsFavorite(_currentHeroItem.Id);
            HeroFavBtn.Content = isFav ? "♥  В избранном" : "♡  В избранное";
            HeroFavBtn.BorderBrush = isFav
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["BorderBrush2"];
            HeroFavBtn.Foreground = isFav
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["TextBrush"];
        }

        private void HeroSwitchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_heroCandidates.Count == 0) return;
            _currentHeroIndex = (_currentHeroIndex + 1) % _heroCandidates.Count;
            ShowHeroItem(_heroCandidates[_currentHeroIndex], animate: true);
        }

        private void HeroPrimaryBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHeroItem != null) ExternalLinks.Open(_currentHeroItem);
        }

        private void HeroFavBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHeroItem is null) return;
            _data.ToggleFavorite(_currentHeroItem.Id);
            var isFav = _data.IsFavorite(_currentHeroItem.Id);
            UpdateHeroFavButton();

            if (isFav)
                _app.ShowToast("Добавлено в избранное", "♥");
            else
                _app.ShowToast("Удалено из избранного", "♡");

            _app.OnFavoriteChanged(_currentHeroItem);
        }

        private void HeroPoster_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_currentHeroItem != null)
                _app.OpenItem(_currentHeroItem);
        }

        // ------------------------------------------------------------------
        // Сетки карточек и адаптивность
        // ------------------------------------------------------------------
        // ------------------------------------------------------------------
        // Сетки карточек и адаптивность (1920: ~5, 1600: ~4, 1366: ~3-4)
        // ------------------------------------------------------------------
        private const double CardGap = 16.0;

        private static int CalculateColumns(double width)
        {
            if (width >= 1150) return 5;
            if (width >= 750) return 4;
            if (width >= 520) return 3;
            return 2;
        }

        private void PopulateGrid(UniformGrid container, List<MediaItem> items, int columns)
        {
            container.Children.Clear();
            var halfGap = CardGap / 2.0;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var card = new CardControl
                {
                    Width = double.NaN,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(halfGap, 0, halfGap, 0)
                };
                card.Initialize(item, _data);

                // При клике открываем детальную страницу произведения
                card.OpenRequested += it =>
                {
                    _app.OpenItem(it);
                };

                // При добавлении/удалении из избранного
                card.FavoriteChanged += _ =>
                {
                    UpdateHeroFavButton();
                    _app.OnFavoriteChanged(item);
                };

                container.Children.Add(card);
            }
        }

        private double GetAvailableContentWidth()
        {
            var width = MainScroll?.ActualWidth ?? 0;
            if (width <= 0) width = 900;
            return Math.Max(300, width - 64);
        }

        private void MainScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveWidth(Math.Max(300, e.NewSize.Width - 64));
        }

        private void ApplyResponsiveWidth(double availableWidth)
        {
            if (availableWidth <= 0 || RecsGrid == null) return;

            var columns = CalculateColumns(availableWidth);
            if (columns == _currentColumns) return;
            _currentColumns = columns;

            RecsGrid.Columns = columns;
            PopularGrid.Columns = columns;
            NewGrid.Columns = columns;

            if (_recsItems.Count > 0)
                PopulateGrid(RecsGrid, _recsItems.Take(columns).ToList(), columns);
            if (_popularItems.Count > 0)
                PopulateGrid(PopularGrid, _popularItems.Take(columns).ToList(), columns);
            if (_newItems.Count > 0)
                PopulateGrid(NewGrid, _newItems.Take(columns).ToList(), columns);
        }

        // ------------------------------------------------------------------
        // Поиск (Выпадающий блок результатов на Главной странице)
        // ------------------------------------------------------------------
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var hasText = !string.IsNullOrWhiteSpace(SearchBox.Text);
            SearchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;

            if (hasText)
            {
                if (ClearSearchBtn.Visibility != Visibility.Visible)
                {
                    ClearSearchBtn.Visibility = Visibility.Visible;
                    ClearSearchBtn.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(150)));
                }
            }
            else
            {
                if (ClearSearchBtn.Visibility == Visibility.Visible)
                {
                    var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(150));
                    fadeOut.Completed += (_, _) => ClearSearchBtn.Visibility = Visibility.Collapsed;
                    ClearSearchBtn.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                }
            }

            if (!hasText)
            {
                _searchDebounceTimer.Stop();
                SearchPopup.IsOpen = false;
                return;
            }

            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _lostFocusTimer.Stop();
            var accent = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
            SearchBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(accent.Color, TimeSpan.FromMilliseconds(180)));
            SearchPlaceholder.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.35, TimeSpan.FromMilliseconds(180)));

            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                PerformSearch(SearchBox.Text.Trim());
                SearchPopup.IsOpen = true;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var border2 = (SolidColorBrush)Application.Current.Resources["BorderBrush2"];
            SearchBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(border2.Color, TimeSpan.FromMilliseconds(180)));
            SearchPlaceholder.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.8, TimeSpan.FromMilliseconds(180)));

            _lostFocusTimer.Stop();
            _lostFocusTimer.Start();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchPopup.IsOpen = false;
                _selectedDropdownIndex = -1;
                UpdateDropdownSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Down && SearchPopup.IsOpen && _dropdownItems.Count > 0)
            {
                _selectedDropdownIndex = (_selectedDropdownIndex + 1) % _dropdownItems.Count;
                UpdateDropdownSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Up && SearchPopup.IsOpen && _dropdownItems.Count > 0)
            {
                _selectedDropdownIndex = (_selectedDropdownIndex - 1 + _dropdownItems.Count) % _dropdownItems.Count;
                UpdateDropdownSelection();
                e.Handled = true;
            }
            else if (e.Key == Key.Enter)
            {
                if (SearchPopup.IsOpen && _selectedDropdownIndex >= 0 && _selectedDropdownIndex < _dropdownItems.Count)
                {
                    var selected = _dropdownItems[_selectedDropdownIndex].item;
                    SearchPopup.IsOpen = false;
                    _app.OpenItem(selected);
                    e.Handled = true;
                    return;
                }

                var query = SearchBox.Text?.Trim() ?? "";
                if (!string.IsNullOrWhiteSpace(query))
                {
                    SearchPopup.IsOpen = false;
                    var moviesCount = _data.Search(query, _data.Movies).Count;
                    var booksCount = _data.Search(query, _data.Books).Count;
                    if (booksCount > moviesCount)
                        _app.ShowPage("books", searchQuery: query);
                    else
                        _app.ShowPage("movies", searchQuery: query);
                    e.Handled = true;
                }
            }
        }

        private void UpdateDropdownSelection()
        {
            for (int i = 0; i < _dropdownItems.Count; i++)
            {
                var (border, _) = _dropdownItems[i];
                if (i == _selectedDropdownIndex)
                {
                    border.Background = (Brush)Application.Current.Resources["CardHoverBrush"];
                    border.BorderBrush = (Brush)Application.Current.Resources["AccentBrush"];
                }
                else
                {
                    border.Background = Brushes.Transparent;
                    border.BorderBrush = Brushes.Transparent;
                }
            }
        }

        private void SearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            var query = SearchBox.Text?.Trim() ?? "";
            PerformSearch(query);
        }

        private void PerformSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                SearchPopup.IsOpen = false;
                _selectedDropdownIndex = -1;
                _dropdownItems.Clear();
                return;
            }

            var movies = _data.Search(query, _data.Movies);
            var books = _data.Search(query, _data.Books);
            var total = movies.Count + books.Count;

            _dropdownItems.Clear();
            _selectedDropdownIndex = -1;
            DropdownMoviesList.Children.Clear();
            DropdownBooksList.Children.Clear();

            DropdownCountText.Text = $"Найдено: {total}";

            if (total == 0)
            {
                DropdownEmptyState.Visibility = Visibility.Visible;
                DropdownEmptyTranslate.Y = -8;
                DropdownEmptyState.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180)));
                DropdownEmptyTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-8.0, 0.0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
                DropdownMoviesSection.Visibility = Visibility.Collapsed;
                DropdownSeparator.Visibility = Visibility.Collapsed;
                DropdownBooksSection.Visibility = Visibility.Collapsed;
                DropdownFooter.Visibility = Visibility.Collapsed;
                SearchPopup.IsOpen = true;
                return;
            }

            DropdownEmptyState.Visibility = Visibility.Collapsed;

            // Фильмы (топ-3)
            if (movies.Count > 0)
            {
                DropdownMoviesSection.Visibility = Visibility.Visible;
                foreach (var movie in movies.Take(3))
                {
                    var itemBorder = CreateDropdownItem(movie);
                    DropdownMoviesList.Children.Add(itemBorder);
                    _dropdownItems.Add((itemBorder, movie));
                }
            }
            else
            {
                DropdownMoviesSection.Visibility = Visibility.Collapsed;
            }

            // Книги (топ-3)
            if (books.Count > 0)
            {
                DropdownBooksSection.Visibility = Visibility.Visible;
                foreach (var book in books.Take(3))
                {
                    var itemBorder = CreateDropdownItem(book);
                    DropdownBooksList.Children.Add(itemBorder);
                    _dropdownItems.Add((itemBorder, book));
                }
            }
            else
            {
                DropdownBooksSection.Visibility = Visibility.Collapsed;
            }

            DropdownSeparator.Visibility = (movies.Count > 0 && books.Count > 0)
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Подвал
            DropdownFooter.Visibility = Visibility.Visible;
            ShowAllMoviesBtn.Content = $"Все фильмы ({movies.Count}) →";
            ShowAllMoviesBtn.Visibility = movies.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ShowAllBooksBtn.Content = $"Все книги ({books.Count}) →";
            ShowAllBooksBtn.Visibility = books.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            SearchPopup.IsOpen = true;
        }

        private Border CreateDropdownItem(MediaItem item)
        {
            var border = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand
            };

            border.MouseEnter += (_, _) =>
            {
                _selectedDropdownIndex = _dropdownItems.FindIndex(x => x.border == border);
                UpdateDropdownSelection();
            };
            border.MouseLeave += (_, _) =>
            {
                if (_selectedDropdownIndex != _dropdownItems.FindIndex(x => x.border == border))
                {
                    border.Background = Brushes.Transparent;
                    border.BorderBrush = Brushes.Transparent;
                }
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var posterBorder = new Border
            {
                Width = 28,
                Height = 40,
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 10, 0),
                Background = (Brush)Application.Current.Resources["BgBrush"]
            };
            posterBorder.Child = new Image
            {
                Source = PosterService.GetImage(item),
                Stretch = Stretch.UniformToFill
            };
            Grid.SetColumn(posterBorder, 0);
            grid.Children.Add(posterBorder);

            var textPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var title = new TextBlock
            {
                Text = item.Title,
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            textPanel.Children.Add(title);

            var subtitle = new TextBlock
            {
                Style = (Style)Application.Current.Resources["SmallText"],
                Foreground = (Brush)Application.Current.Resources["TextMutedBrush"],
                Margin = new Thickness(0, 2, 0, 0)
            };
            if (item.Kind == MediaKind.Movie)
            {
                subtitle.Text = $"★ {item.Rating:F1}" + (item.Year > 0 ? $"  •  {item.Year} г." : "");
            }
            else
            {
                subtitle.Text = (!string.IsNullOrWhiteSpace(item.Author) ? item.Author : item.GenreText) + $"  •  ★ {item.Rating:F1}";
            }
            textPanel.Children.Add(subtitle);

            Grid.SetColumn(textPanel, 1);
            grid.Children.Add(textPanel);

            border.Child = grid;
            border.MouseLeftButtonUp += (_, _) =>
            {
                SearchPopup.IsOpen = false;
                _app.OpenItem(item);
            };

            return border;
        }

        private void ShowAllMoviesBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchPopup.IsOpen = false;
            var query = SearchBox.Text?.Trim() ?? "";
            _app.ShowPage("movies", searchQuery: query);
        }

        private void ShowAllBooksBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchPopup.IsOpen = false;
            var query = SearchBox.Text?.Trim() ?? "";
            _app.ShowPage("books", searchQuery: query);
        }

        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchPlaceholder.Visibility = Visibility.Visible;
            ClearSearchBtn.Visibility = Visibility.Collapsed;
            SearchPopup.IsOpen = false;
            SearchBox.Focus();
        }
    }
}

