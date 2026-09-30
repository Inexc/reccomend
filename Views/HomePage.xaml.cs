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
        private double _currentCardWidth = 190;

        public HomePage()
        {
            InitializeComponent();

            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(160)
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        }

        public void Initialize(DataManager data, MainWindow app)
        {
            _data = data;
            _app = app;

            RefreshData();
        }

        public void RefreshData()
        {
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

            // 1. Секция «Рекомендуем для вас» (4 карточки)
            var recs = RecommendationService.BuildPersonalizedFeed(allItems, _data.Favorites, limit: 4, excludeIds: usedIds);
            foreach (var r in recs) usedIds.Add(r.Id);

            RecsSubtitle.Text = _data.Favorites.Count > 0
                ? "На основе ваших предпочтений"
                : "На основе рейтинга и популярности";

            if (recs.Count == 0)
            {
                RecsEmptyBanner.Visibility = Visibility.Visible;
                RecsGrid.Visibility = Visibility.Collapsed;
            }
            else
            {
                RecsEmptyBanner.Visibility = Visibility.Collapsed;
                RecsGrid.Visibility = Visibility.Visible;
                PopulateGrid(RecsGrid, recs);
            }

            // 2. Секция «Популярное» (4 карточки)
            var popular = RecommendationService.BuildPopularFeed(allItems, excludeIds: usedIds, limit: 4);
            foreach (var p in popular) usedIds.Add(p.Id);
            PopulateGrid(PopularGrid, popular);

            // 3. Секция «Новинки» (4 карточки)
            var newReleases = RecommendationService.BuildNewReleasesFeed(allItems, excludeIds: usedIds, limit: 4);
            PopulateGrid(NewGrid, newReleases);

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

        // ------------------------------------------------------------------
        // Сетки карточек и адаптивность
        // ------------------------------------------------------------------
        private void PopulateGrid(UniformGrid container, List<MediaItem> items)
        {
            container.Children.Clear();
            var columns = container.Columns > 0 ? container.Columns : 4;
            var gap = 14.0;

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var card = new CardControl
                {
                    Width = _currentCardWidth,
                    Margin = ((i + 1) % columns == 0)
                        ? new Thickness(0, 0, 0, 16)
                        : new Thickness(0, 0, gap, 16)
                };
                card.Initialize(item, _data);

                // При клике открываем в DetailsPanel (не дублируя Hero)
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
            return Math.Max(300, width - 96);
        }

        private void MainScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveWidth(Math.Max(300, e.NewSize.Width - 96));
        }

        private void ApplyResponsiveWidth(double availableWidth)
        {
            if (availableWidth <= 0 || RecsGrid == null) return;

            int columns = availableWidth >= 680 ? 4 : 2;
            double cardGap = 14.0;
            double widthPerCard = Math.Floor((availableWidth - ((columns - 1) * cardGap)) / columns);
            widthPerCard = Math.Min(Math.Max(widthPerCard, 145), 245);
            _currentCardWidth = widthPerCard;

            RecsGrid.Columns = columns;
            PopularGrid.Columns = columns;
            NewGrid.Columns = columns;

            UpdateCardsInGrid(RecsGrid, columns, cardGap, widthPerCard);
            UpdateCardsInGrid(PopularGrid, columns, cardGap, widthPerCard);
            UpdateCardsInGrid(NewGrid, columns, cardGap, widthPerCard);
        }

        private static void UpdateCardsInGrid(UniformGrid grid, int columns, double cardGap, double widthPerCard)
        {
            if (grid == null) return;
            for (int i = 0; i < grid.Children.Count; i++)
            {
                if (grid.Children[i] is CardControl card)
                {
                    card.Width = widthPerCard;
                    card.Margin = ((i + 1) % columns == 0)
                        ? new Thickness(0, 0, 0, 16)
                        : new Thickness(0, 0, cardGap, 16);
                }
            }
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

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (!SearchBox.IsKeyboardFocusWithin)
                    SearchPopup.IsOpen = false;
            };
            timer.Start();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                SearchPopup.IsOpen = false;
            }
            else if (e.Key == Key.Enter)
            {
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
                return;
            }

            var movies = _data.Search(query, _data.Movies);
            var books = _data.Search(query, _data.Books);
            var total = movies.Count + books.Count;

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
                    DropdownMoviesList.Children.Add(CreateDropdownItem(movie));
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
                    DropdownBooksList.Children.Add(CreateDropdownItem(book));
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

        private FrameworkElement CreateDropdownItem(MediaItem item)
        {
            var border = new Border
            {
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 1, 0, 1),
                Cursor = Cursors.Hand
            };

            border.MouseEnter += (_, _) => border.Background = (Brush)Application.Current.Resources["CardHoverBrush"];
            border.MouseLeave += (_, _) => border.Background = Brushes.Transparent;

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
                ShowHeroItem(item, animate: true);
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

