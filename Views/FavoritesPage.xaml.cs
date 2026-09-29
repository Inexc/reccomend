using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    public partial class FavoritesPage : UserControl
    {
        private DataManager _data = null!;
        private MainWindow _app = null!;
        private string _currentFilter = "all";

        private const double CardGap = 14;
        private const double MinCardWidth = 168;
        private const double MaxCardWidth = 220;

        public FavoritesPage()
        {
            InitializeComponent();
        }

        public void Initialize(DataManager data, MainWindow app)
        {
            _data = data;
            _app = app;
            FilterAllBtn.Click -= Filter_Click;
            FilterMoviesBtn.Click -= Filter_Click;
            FilterBooksBtn.Click -= Filter_Click;
            FilterAllBtn.Click += Filter_Click;
            FilterMoviesBtn.Click += Filter_Click;
            FilterBooksBtn.Click += Filter_Click;
            Refresh();
        }

        private void CardsScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveWidth(e.NewSize.Width);
        }

        private void ApplyResponsiveWidth(double availableWidth)
        {
            if (availableWidth <= 0) return;

            var columns = Math.Max(1, (int)((availableWidth + CardGap) / (MinCardWidth + CardGap)));
            var itemWidth = Math.Min(availableWidth / columns - CardGap, MaxCardWidth);

            foreach (var child in CardsPanel.Children)
            {
                if (child is CardControl card)
                    card.Width = itemWidth;
            }
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (sender == FilterAllBtn) _currentFilter = "all";
            else if (sender == FilterMoviesBtn) _currentFilter = "movies";
            else if (sender == FilterBooksBtn) _currentFilter = "books";

            UpdateFilterButtons();
            Refresh();
        }

        private void UpdateFilterButtons()
        {
            var allStyle = _currentFilter == "all" ? (Style)Application.Current.Resources["GenreChip"] : (Style)Application.Current.Resources["GenreChip"];
            var moviesStyle = _currentFilter == "movies" ? (Style)Application.Current.Resources["GenreChip"] : (Style)Application.Current.Resources["GenreChip"];
            var booksStyle = _currentFilter == "books" ? (Style)Application.Current.Resources["GenreChip"] : (Style)Application.Current.Resources["GenreChip"];

            FilterAllBtn.BorderBrush = _currentFilter == "all" ? (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"] : (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush2"];
            FilterMoviesBtn.BorderBrush = _currentFilter == "movies" ? (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"] : (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush2"];
            FilterBooksBtn.BorderBrush = _currentFilter == "books" ? (System.Windows.Media.Brush)Application.Current.Resources["AccentBrush"] : (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush2"];
        }

        public void Refresh()
        {
            CardsPanel.Children.Clear();
            var allItems = _data.GetFavoriteItems();

            var items = _currentFilter switch
            {
                "movies" => allItems.FindAll(x => x.Kind == Models.MediaKind.Movie),
                "books" => allItems.FindAll(x => x.Kind == Models.MediaKind.Book),
                _ => allItems
            };

            UpdateStatistics(allItems);

            if (items.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                CardsScroll.Visibility = Visibility.Collapsed;
                return;
            }

            EmptyState.Visibility = Visibility.Collapsed;
            CardsScroll.Visibility = Visibility.Visible;

            var availableWidth = CardsScroll.ActualWidth;
            double? presetWidth = null;
            if (availableWidth > 0)
            {
                var columns = Math.Max(1, (int)((availableWidth + CardGap) / (MinCardWidth + CardGap)));
                presetWidth = Math.Min(availableWidth / columns - CardGap, MaxCardWidth);
            }

            foreach (var item in items)
            {
                var card = new CardControl { Margin = new Thickness(0, 0, CardGap, CardGap) };
                if (presetWidth.HasValue) card.Width = presetWidth.Value;
                card.Initialize(item, _data);
                card.OpenRequested += _app.OpenItem;
                card.FavoriteChanged += _ => Refresh();
                CardsPanel.Children.Add(card);
            }

            // Плавная анимация обновления
            var anim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            CardsPanel.BeginAnimation(OpacityProperty, anim);
        }

        private void GoToMovies_Click(object sender, RoutedEventArgs e) => _app.ShowPage("movies");
        private void GoToBooks_Click(object sender, RoutedEventArgs e) => _app.ShowPage("books");

        private void UpdateStatistics(System.Collections.Generic.List<Models.MediaItem> items)
        {
            var movieCount = items.FindAll(x => x.Kind == Models.MediaKind.Movie).Count;
            var bookCount = items.FindAll(x => x.Kind == Models.MediaKind.Book).Count;
            var totalCount = items.Count;

            MoviesCountText.Text = movieCount.ToString();
            BooksCountText.Text = bookCount.ToString();
            TotalCountText.Text = totalCount.ToString();
        }
    }
}
