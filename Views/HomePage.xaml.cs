using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
        private MediaItem? _heroItem;
        private readonly DispatcherTimer _searchDebounceTimer;

        public HomePage()
        {
            InitializeComponent();
            RecsScroll.PreviewMouseWheel += Carousel_PreviewMouseWheel;
            NewScroll.PreviewMouseWheel += Carousel_PreviewMouseWheel;

            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(160)
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
        }

        private static void Carousel_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scroll = (ScrollViewer)sender;
            scroll.ScrollToHorizontalOffset(scroll.HorizontalOffset - e.Delta);
            e.Handled = true;
        }

        public void Initialize(DataManager data, MainWindow app)
        {
            _data = data;
            _app = app;

            RefreshData();
        }

        public void RefreshData()
        {
            SetHero(RecommendationService.PickFeatured(_data.AllItems(), _data.Favorites));
            BuildFavoritesList();
            BuildGenreChips();
            BuildCarousel(RecsRow, RecommendationService.BuildPersonalizedFeed(_data.AllItems(), _data.Favorites, 12));
            BuildCarousel(NewRow, _data.AllItems().OrderByDescending(i => i.Year).Take(12));

            // Если поиск активен — обновляем результаты поиска
            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                PerformSearch(SearchBox.Text);
            }
        }

        // ------------------------------------------------------------------
        // Hero
        // ------------------------------------------------------------------
        private void SetHero(MediaItem? item)
        {
            if (item is null) return;

            if (_heroItem != null && _heroItem.Id != item.Id)
            {
                var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(80));
                fadeOut.Completed += (_, _) =>
                {
                    ApplyHeroData(item);
                    var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(150))
                    {
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    HeroFrame.BeginAnimation(OpacityProperty, fadeIn);
                };
                HeroFrame.BeginAnimation(OpacityProperty, fadeOut);
            }
            else
            {
                ApplyHeroData(item);
            }
        }

        private void ApplyHeroData(MediaItem item)
        {
            _heroItem = item;

            HeroGenres.Children.Clear();
            foreach (var genre in item.Genre)
            {
                HeroGenres.Children.Add(new Border
                {
                    Background = (Brush)Application.Current.Resources["OverlayBrush"],
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12, 5, 12, 5),
                    Margin = new Thickness(0, 0, 8, 0),
                    Child = new TextBlock
                    {
                        Text = genre.ToUpper(),
                        FontSize = 10,
                        FontFamily = new FontFamily("Segoe UI Semibold"),
                        Foreground = (Brush)Application.Current.Resources["Accent2Brush"]
                    }
                });
            }

            HeroTitle.Text = item.Title;
            HeroAuthor.Text = item.Author;
            HeroRating.Text = $"{item.Rating:F1}";
            HeroDescription.Text = item.Description;
            HeroPrimaryBtn.Content = item.Kind == MediaKind.Movie ? "▷  Смотреть" : "▷  Читать";
            UpdateHeroFavButton();

            HeroImage.Source = PosterService.GetImage(item);
        }

        private void UpdateHeroFavButton()
        {
            if (_heroItem is null) return;
            var isFav = _data.IsFavorite(_heroItem.Id);
            HeroFavBtn.Content = isFav ? "♥  В избранном" : "♡  В избранное";
            HeroFavBtn.BorderBrush = isFav
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["BorderBrush2"];
        }

        private void HeroPrimaryBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_heroItem != null) ExternalLinks.Open(_heroItem);
        }

        private void HeroFavBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_heroItem is null) return;
            _data.ToggleFavorite(_heroItem.Id);
            var isFav = _data.IsFavorite(_heroItem.Id);
            UpdateHeroFavButton();
            BuildFavoritesList();

            if (isFav)
                _app.ShowToast("Добавлено в избранное", "♥");
            else
                _app.ShowToast("Удалено из избранного", "♡");

            _app.OnFavoriteChanged(_heroItem);
        }

        // ------------------------------------------------------------------
        // Из избранного
        // ------------------------------------------------------------------
        private void BuildFavoritesList()
        {
            FavoritesList.Children.Clear();
            var favorites = _data.GetFavoriteItems().Take(4).ToList();
            FavoritesEmptyText.Visibility = favorites.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            foreach (var item in favorites)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 10), Cursor = Cursors.Hand };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var posterBorder = new Border
                {
                    Width = 38,
                    Height = 52,
                    CornerRadius = new CornerRadius(6),
                    ClipToBounds = true,
                    Margin = new Thickness(0, 0, 10, 0),
                    Background = (Brush)Application.Current.Resources["BgBrush"]
                };
                var image = new Image
                {
                    Stretch = Stretch.UniformToFill,
                    Source = PosterService.GetImage(item)
                };
                posterBorder.Child = image;
                Grid.SetColumn(posterBorder, 0);

                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textStack.Children.Add(new TextBlock
                {
                    Text = item.Title.Length > 22 ? item.Title[..20] + "…" : item.Title,
                    FontSize = 12,
                    FontFamily = new FontFamily("Segoe UI Semibold"),
                    Foreground = (Brush)Application.Current.Resources["TextBrush"]
                });
                textStack.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(item.Author) ? item.GenreText : item.Author,
                    FontSize = 10,
                    Foreground = (Brush)Application.Current.Resources["TextFaintBrush"],
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Grid.SetColumn(textStack, 1);

                row.Children.Add(posterBorder);
                row.Children.Add(textStack);
                row.MouseLeftButtonUp += (_, _) =>
                {
                    SetHero(item);
                    _app.OpenItem(item);
                };

                FavoritesList.Children.Add(row);
            }
        }

        // ------------------------------------------------------------------
        // Жанры
        // ------------------------------------------------------------------
        private void BuildGenreChips()
        {
            GenreChipsPanel.Children.Clear();
            var genres = _data.GenresFor(_data.AllItems()).Take(8);
            foreach (var genre in genres)
            {
                var chip = new Button
                {
                    Content = genre,
                    Style = (Style)Application.Current.Resources["GenreChip"],
                    Margin = new Thickness(0, 0, 8, 8)
                };
                chip.Click += (_, _) => _app.ShowPage("movies", genreFilter: genre);
                GenreChipsPanel.Children.Add(chip);
            }
        }

        // ------------------------------------------------------------------
        // Горизонтальные карусели
        // ------------------------------------------------------------------
        private void BuildCarousel(StackPanel row, IEnumerable<MediaItem> items)
        {
            row.Children.Clear();
            foreach (var item in items)
            {
                var card = new CardControl { Margin = new Thickness(0, 0, 16, 4) };
                card.Initialize(item, _data);
                card.OpenRequested += it =>
                {
                    SetHero(it);
                    _app.OpenItem(it);
                };
                card.FavoriteChanged += _ =>
                {
                    BuildFavoritesList();
                    UpdateHeroFavButton();
                    _app.OnFavoriteChanged(item);
                };
                row.Children.Add(card);
            }
        }

        private void RecsLeft_Click(object sender, RoutedEventArgs e) =>
            AnimateScroll(RecsScroll, RecsScroll.HorizontalOffset - 420);

        private void RecsRight_Click(object sender, RoutedEventArgs e) =>
            AnimateScroll(RecsScroll, RecsScroll.HorizontalOffset + 420);

        private void NewLeft_Click(object sender, RoutedEventArgs e) =>
            AnimateScroll(NewScroll, NewScroll.HorizontalOffset - 420);

        private void NewRight_Click(object sender, RoutedEventArgs e) =>
            AnimateScroll(NewScroll, NewScroll.HorizontalOffset + 420);

        private static void AnimateScroll(ScrollViewer scrollViewer, double targetOffset)
        {
            targetOffset = Math.Max(0, Math.Min(targetOffset, scrollViewer.ScrollableWidth));
            scrollViewer.ScrollToHorizontalOffset(targetOffset);
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
                SetHero(item);
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

