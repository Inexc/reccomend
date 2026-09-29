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
                _app.ShowToast($"«{_heroItem.Title}» добавлено в избранное", "♥");
            else
                _app.ShowToast($"«{_heroItem.Title}» удалено из избранного", "♡");

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
        // Поиск (Живой поиск по фильмам и книгам одновременно)
        // ------------------------------------------------------------------
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
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
                ClearSearchBtn.Visibility = Visibility.Collapsed;
                SearchResultsPanel.Visibility = Visibility.Collapsed;
                DefaultContentPanel.Visibility = Visibility.Visible;
                return;
            }

            ClearSearchBtn.Visibility = Visibility.Visible;
            DefaultContentPanel.Visibility = Visibility.Collapsed;
            SearchResultsPanel.Visibility = Visibility.Visible;

            var results = _data.Search(query, _data.AllItems());
            SearchResultsCards.Children.Clear();

            if (results.Count == 0)
            {
                SearchResultsSubtitle.Text = $"По запросу «{query}» ничего не найдено";
                SearchEmptyState.Visibility = Visibility.Visible;
                _app.ShowToast("Ничего не найдено", "🔍");
            }
            else
            {
                SearchEmptyState.Visibility = Visibility.Collapsed;
                SearchResultsSubtitle.Text = $"Найдено произведений: {results.Count} (фильмы и книги)";

                foreach (var item in results)
                {
                    var card = new CardControl { Margin = new Thickness(0, 0, 16, 16) };
                    card.Initialize(item, _data);
                    card.OpenRequested += it =>
                    {
                        SetHero(it);
                        _app.OpenItem(it);
                    };
                    card.FavoriteChanged += _ => _app.OnFavoriteChanged(item);
                    SearchResultsCards.Children.Add(card);
                }

                // Плавное появление результатов (200ms)
                var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                SearchResultsCards.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
        }

        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            PerformSearch("");
        }
    }
}
