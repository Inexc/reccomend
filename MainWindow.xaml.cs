using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MovieBookRecommender.Models;
using MovieBookRecommender.Services;
using MovieBookRecommender.Views;

namespace MovieBookRecommender
{
    public partial class MainWindow : Window
    {
        private readonly DataManager _data = new();
        private readonly Dictionary<string, UserControl> _pages = new();
        private string _currentPageKey = "home";

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _data.Load();

            LoadingOverlay.Visibility = Visibility.Visible;
            await EnsureAllPostersAsync();
            LoadingOverlay.Visibility = Visibility.Collapsed;

            DetailsPanelControl.Initialize(_data);
            DetailsPanelControl.OpenRequested += OpenItem;
            DetailsPanelControl.RefreshRequested += ShowFeaturedDefault;

            ShowFeaturedDefault();
            ShowPage("home");
        }

        /// <summary>Скачивает/генерирует постеры для всех элементов параллельно
        /// при первом запуске (дальше файлы уже лежат на диске).</summary>
        private async Task EnsureAllPostersAsync()
        {
            var tasks = _data.AllItems().Select(PosterService.EnsurePosterAsync);
            await Task.WhenAll(tasks);
        }

        private void ShowFeaturedDefault()
        {
            var featured = RecommendationService.PickFeatured(_data.AllItems(), _data.Favorites);
            DetailsPanelControl.ShowItem(featured);
        }

        private void SidebarControl_Navigate(string key) => ShowPage(key);

        public void ShowPage(string key, string? searchQuery = null, string? genreFilter = null)
        {
            if (!_pages.ContainsKey(key))
                _pages[key] = CreatePage(key);

            var page = _pages[key];
            CenterHost.Children.Clear();
            CenterHost.Children.Add(page);
            _currentPageKey = key;

            switch (key)
            {
                case "movies" or "books" when !string.IsNullOrEmpty(genreFilter):
                    ((ListPage)page).SetGenreFilter(genreFilter!);
                    break;
                case "movies" or "books" when !string.IsNullOrEmpty(searchQuery):
                    ((ListPage)page).SetSearchQuery(searchQuery!);
                    break;
                case "movies" or "books":
                    ((ListPage)page).Refresh();
                    break;
                case "favorites":
                    ((FavoritesPage)page).Refresh();
                    break;
            }

            SidebarControl.SetActive(key);
        }

        private UserControl CreatePage(string key)
        {
            switch (key)
            {
                case "home":
                    var home = new HomePage();
                    home.Initialize(_data, this);
                    return home;
                case "movies":
                    var moviesPage = new ListPage();
                    moviesPage.Initialize(_data, this, () => _data.Movies, "Фильмы", "🎞");
                    return moviesPage;
                case "books":
                    var booksPage = new ListPage();
                    booksPage.Initialize(_data, this, () => _data.Books, "Книги", "📖");
                    return booksPage;
                case "favorites":
                    var favPage = new FavoritesPage();
                    favPage.Initialize(_data, this);
                    return favPage;
                case "settings":
                    var settingsPage = new SettingsPage();
                    settingsPage.Initialize(_data, this);
                    return settingsPage;
                default:
                    throw new ArgumentException($"Неизвестный раздел: {key}");
            }
        }

        public void RefreshCurrentPage()
        {
            if (!_pages.TryGetValue(_currentPageKey, out var page)) return;
            switch (page)
            {
                case ListPage lp: lp.Refresh(); break;
                case FavoritesPage fp: fp.Refresh(); break;
            }
        }

        /// <summary>Открыть подробности объекта в правой панели (вызывается из карточек).</summary>
        public void OpenItem(MediaItem item) => DetailsPanelControl.ShowItem(item);

        /// <summary>Вызывается при клике на ♡/♥ на любой карточке.</summary>
        public void OnFavoriteChanged(MediaItem item)
        {
            if (_currentPageKey == "favorites")
                RefreshCurrentPage();

            if (DetailsPanelControl.CurrentItem != null)
                DetailsPanelControl.ShowItem(DetailsPanelControl.CurrentItem);
        }
    }
}
