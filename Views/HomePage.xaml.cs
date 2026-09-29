using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MovieBookRecommender.Models;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    public partial class HomePage : UserControl
    {
        private DataManager _data = null!;
        private MainWindow _app = null!;
        private MediaItem? _heroItem;

        public HomePage()
        {
            InitializeComponent();
            RecsScroll.PreviewMouseWheel += Carousel_PreviewMouseWheel;
            NewScroll.PreviewMouseWheel += Carousel_PreviewMouseWheel;
        }

        /// <summary>Колесо мыши над горизонтальной каруселью крутит её по горизонтали
        /// (по умолчанию ScrollViewer с отключённой вертикальной прокруткой просто
        /// проглатывает событие колеса и ничего не делает).</summary>
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

            SetHero(RecommendationService.PickFeatured(_data.AllItems(), _data.Favorites));
            BuildFavoritesList();
            BuildGenreChips();
            BuildCarousel(RecsRow, BuildRecommendedPool());
            BuildCarousel(NewRow, _data.AllItems().OrderByDescending(i => i.Year).Take(12));
        }

        private IEnumerable<MediaItem> BuildRecommendedPool()
        {
            // "Рекомендуем для вас" — топ по рейтингу среди обоих типов контента
            return _data.AllItems().OrderByDescending(i => i.Rating).Take(12);
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
                        Text = genre.ToUpper(), FontSize = 10, FontFamily = new FontFamily("Segoe UI Semibold"),
                        Foreground = (Brush)Application.Current.Resources["Accent2Brush"]
                    }
                });
            }

            HeroTitle.Text = item.Title;
            HeroAuthor.Text = item.Author;
            HeroRating.Text = item.Rating.ToString();
            HeroDescription.Text = item.Description;
            HeroPrimaryBtn.Content = item.Kind == MediaKind.Movie ? "▷  Смотреть" : "▷  Читать";
            UpdateHeroFavButton();

            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, item.Poster);
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                HeroImage.Source = bmp;
            }
            catch (IOException) { }
        }

        private void UpdateHeroFavButton()
        {
            if (_heroItem is null) return;
            HeroFavBtn.Content = _data.IsFavorite(_heroItem.Id) ? "♥  В избранном" : "♡  В избранное";
        }

        private void HeroPrimaryBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_heroItem != null) ExternalLinks.Open(_heroItem);
        }

        private void HeroFavBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_heroItem is null) return;
            _data.ToggleFavorite(_heroItem.Id);
            UpdateHeroFavButton();
            BuildFavoritesList();
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

                var posterBorder = new Border { Width = 38, Height = 52, CornerRadius = new CornerRadius(6), ClipToBounds = true, Margin = new Thickness(0, 0, 10, 0) };
                var image = new Image { Stretch = Stretch.UniformToFill };
                var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, item.Poster);
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    image.Source = bmp;
                }
                catch (IOException) { }
                posterBorder.Child = image;
                Grid.SetColumn(posterBorder, 0);

                var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                textStack.Children.Add(new TextBlock
                {
                    Text = item.Title.Length > 22 ? item.Title[..20] + "…" : item.Title,
                    FontSize = 12, FontFamily = new FontFamily("Segoe UI Semibold"),
                    Foreground = (Brush)Application.Current.Resources["TextBrush"]
                });
                textStack.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(item.Author) ? item.GenreText : item.Author,
                    FontSize = 10, Foreground = (Brush)Application.Current.Resources["TextFaintBrush"],
                    Margin = new Thickness(0, 2, 0, 0)
                });
                Grid.SetColumn(textStack, 1);

                row.Children.Add(posterBorder);
                row.Children.Add(textStack);
                row.MouseLeftButtonUp += (_, _) => SetHero(item);

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
                    Content = genre, Style = (Style)Application.Current.Resources["GenreChip"],
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
                card.OpenRequested += SetHero;
                card.FavoriteChanged += _ => { BuildFavoritesList(); UpdateHeroFavButton(); };
                row.Children.Add(card);
            }
        }

        private void RecsLeft_Click(object sender, RoutedEventArgs e) => RecsScroll.ScrollToHorizontalOffset(RecsScroll.HorizontalOffset - 420);
        private void RecsRight_Click(object sender, RoutedEventArgs e) => RecsScroll.ScrollToHorizontalOffset(RecsScroll.HorizontalOffset + 420);
        private void NewLeft_Click(object sender, RoutedEventArgs e) => NewScroll.ScrollToHorizontalOffset(NewScroll.HorizontalOffset - 420);
        private void NewRight_Click(object sender, RoutedEventArgs e) => NewScroll.ScrollToHorizontalOffset(NewScroll.HorizontalOffset + 420);

        // ------------------------------------------------------------------
        // Поиск
        // ------------------------------------------------------------------
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(SearchBox.Text))
                _app.ShowPage("movies", SearchBox.Text);
        }
    }
}
