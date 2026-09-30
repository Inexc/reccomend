using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MovieBookRecommender.Models;
using MovieBookRecommender.Services;
using MovieBookRecommender.Views;

namespace MovieBookRecommender
{
    public partial class MainWindow : Window
    {
        private readonly DataManager _data = new();
        private readonly SettingsManager _settings = new();
        private readonly Dictionary<string, UserControl> _pages = new();
        private string _currentPageKey = "home";
        private DispatcherTimer? _toastTimer;

        public class NavigationEntry
        {
            public string PageKey { get; }
            public MediaItem? DetailItem { get; }
            public string SourceTitle { get; set; } = "";
            public ListPage.ListPageState? ListState { get; set; }
            public FavoritesPage.FavoritesPageState? FavoritesState { get; set; }
            public double ScrollOffset { get; set; }
            public string? SearchQuery { get; set; }
            public string? GenreFilter { get; set; }

            public NavigationEntry(string pageKey, MediaItem? detailItem = null)
            {
                PageKey = pageKey;
                DetailItem = detailItem;
            }
        }

        private readonly Stack<NavigationEntry> _history = new();
        private MediaItem? _currentDetailItem;
        public MediaItem? CurrentDetailItem => _currentDetailItem;

        private UserControl? _currentVisiblePage;
        private int _transitionToken = 0;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closing += (_, _) => _data.Flush();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _data.Load();
                _settings.Load();
                _data.AutoSave = _settings.Settings.AutoSave;

                // Восстановление размеров и состояния окна из сохранённых настроек
                if (_settings.Settings.WindowWidth >= MinWidth)
                    Width = _settings.Settings.WindowWidth;
                if (_settings.Settings.WindowHeight >= MinHeight)
                    Height = _settings.Settings.WindowHeight;
                if (_settings.Settings.IsMaximized)
                    WindowState = WindowState.Maximized;

                ApplyTheme(_settings.Settings.Theme, save: false);
                ApplyScale(_settings.Settings.Scale, save: false);

                LoadingOverlay.Visibility = Visibility.Visible;
                await EnsureAllPostersAsync();
                LoadingOverlay.Visibility = Visibility.Collapsed;

                SidebarControl.ItemReselected += SidebarControl_ItemReselected;
                PreviewKeyDown += MainWindow_PreviewKeyDown;
                PreviewMouseDown += MainWindow_PreviewMouseDown;

                DetailsPanelControl.Initialize(_data);
                DetailsPanelControl.OpenRequested += OpenItem;
                DetailsPanelControl.RefreshRequested += () => ShowFeaturedDefault(shuffle: true);
                DetailsPanelControl.FavoriteChanged += item => OnFavoriteChanged(item);

                ShowFeaturedDefault(shuffle: false);
                ShowPage("home");

                SidebarControl.UpdateFavoritesBadge(_data.Favorites.Count);
            }
            catch (Exception ex)
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                System.Diagnostics.Debug.WriteLine($"[MainWindow_Loaded error]: {ex}");
            }
        }

        private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Alt + Left Arrow для перехода назад
            if (e.SystemKey == Key.Left && (Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            {
                if (_currentPageKey == "detail" || _history.Count > 0)
                {
                    NavigateBack();
                    e.Handled = true;
                }
            }
            // Backspace назад, если фокус не в поле ввода текста
            else if (e.Key == Key.Back && !(e.OriginalSource is TextBox || e.OriginalSource is System.Windows.Controls.Primitives.TextBoxBase))
            {
                if (_currentPageKey == "detail" || _history.Count > 0)
                {
                    NavigateBack();
                    e.Handled = true;
                }
            }
        }

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // Аппаратная кнопка мыши «Назад» (XButton1)
            if (e.ChangedButton == MouseButton.XButton1)
            {
                if (_currentPageKey == "detail" || _history.Count > 0)
                {
                    NavigateBack();
                    e.Handled = true;
                }
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                _settings.Settings.WindowWidth = Width;
                _settings.Settings.WindowHeight = Height;
            }
            else if (RestoreBounds.Width > 0 && RestoreBounds.Height > 0)
            {
                _settings.Settings.WindowWidth = RestoreBounds.Width;
                _settings.Settings.WindowHeight = RestoreBounds.Height;
            }
            _settings.Settings.IsMaximized = WindowState == WindowState.Maximized;
            _settings.Save();

            base.OnClosing(e);
        }

        /// <summary>Скачивает/генерирует постеры для всех элементов параллельно
        /// при первом запуске (дальше файлы уже лежат на диске).</summary>
        private async Task EnsureAllPostersAsync()
        {
            var tasks = _data.AllItems().Select(async item =>
            {
                try
                {
                    await PosterService.EnsurePosterAsync(item);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[EnsurePosterAsync error for {item.Id}]: {ex.Message}");
                }
            });
            await Task.WhenAll(tasks);
        }

        private int _featuredVariety = 0;

        private void ShowFeaturedDefault(bool shuffle = false)
        {
            if (shuffle)
                _featuredVariety++;
            var featured = RecommendationService.PickFeatured(_data.AllItems(), _data.Favorites, _featuredVariety);
            DetailsPanelControl.ShowItem(featured);
        }

        private void SidebarControl_Navigate(string key)
        {
            _currentDetailItem = null;
            ShowPage(key, clearHistory: false);
            ShowFeaturedDefault(shuffle: false);
        }

        private void SidebarControl_ItemReselected(string key)
        {
            // Если пользователь кликнул на уже активный пункт меню
            if (_currentPageKey == "detail")
            {
                _currentDetailItem = null;
                ShowPage(key);
            }
            else if (_pages.TryGetValue(key, out var page))
            {
                switch (page)
                {
                    case ListPage lp: lp.ScrollToTop(); break;
                    case HomePage hp: hp.ScrollToTop(); break;
                    case FavoritesPage fp: fp.ScrollToTop(); break;
                    case DetailPage dp: dp.ScrollToTop(); break;
                }
            }
        }

        /// <summary>
        /// Безупречный бесшовный переход между страницами (180ms fade) без мерцания и белых/чёрных вспышек.
        /// Старый экран не удаляется мгновенно, а плавно растворяется под новым.
        /// </summary>
        private void TransitionToPage(UserControl newPage, string key)
        {
            _currentPageKey = key;
            int token = ++_transitionToken;

            if (_currentVisiblePage == newPage)
            {
                newPage.BeginAnimation(UIElement.OpacityProperty, null);
                newPage.Opacity = 1.0;
                newPage.Visibility = Visibility.Visible;
                Panel.SetZIndex(newPage, 1);

                for (int i = CenterHost.Children.Count - 1; i >= 0; i--)
                {
                    var child = CenterHost.Children[i];
                    if (child != newPage)
                    {
                        if (child is UIElement ui)
                            ui.BeginAnimation(UIElement.OpacityProperty, null);
                        CenterHost.Children.RemoveAt(i);
                    }
                }
                return;
            }

            var oldPage = _currentVisiblePage;
            _currentVisiblePage = newPage;
            bool enableAnimations = _settings.Settings.EnableAnimations;

            newPage.BeginAnimation(UIElement.OpacityProperty, null);

            // Добавляем новую страницу в Grid без предварительной очистки, чтобы пользователь не видел пустой фон
            if (!CenterHost.Children.Contains(newPage))
            {
                CenterHost.Children.Add(newPage);
            }

            Panel.SetZIndex(newPage, 1);
            newPage.Visibility = Visibility.Visible;

            if (!enableAnimations)
            {
                newPage.Opacity = 1.0;

                if (oldPage != null && oldPage != newPage)
                {
                    oldPage.BeginAnimation(UIElement.OpacityProperty, null);
                    CenterHost.Children.Remove(oldPage);
                }
                return;
            }

            newPage.Opacity = 0.0;

            var easeOut = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var easeIn = new QuadraticEase { EasingMode = EasingMode.EaseIn };

            // Плавное появление новой страницы (180ms)
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = easeOut
            };

            // Параллельное затухание старой страницы (140ms)
            if (oldPage != null && oldPage != newPage)
            {
                Panel.SetZIndex(oldPage, 0);
                var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(140))
                {
                    EasingFunction = easeIn
                };
                oldPage.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            }

            fadeIn.Completed += (_, _) =>
            {
                if (token != _transitionToken) return;

                newPage.BeginAnimation(UIElement.OpacityProperty, null);
                newPage.Opacity = 1.0;

                // Очищаем предыдущие страницы из визуального дерева контейнера
                for (int i = CenterHost.Children.Count - 1; i >= 0; i--)
                {
                    var child = CenterHost.Children[i];
                    if (child != newPage)
                    {
                        if (child is UIElement ui)
                            ui.BeginAnimation(UIElement.OpacityProperty, null);
                        CenterHost.Children.RemoveAt(i);
                    }
                }
            };

            newPage.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        public void NavigateToGenreFromDetail(string targetKey, string genre)
        {
            var entry = CaptureCurrentState();
            _history.Push(entry);
            _currentDetailItem = null;
            ShowPage(targetKey, genreFilter: genre, clearHistory: false);
            ShowFeaturedDefault(shuffle: false);
        }

        private NavigationEntry CaptureCurrentState()
        {
            var entry = new NavigationEntry(_currentPageKey, _currentDetailItem);

            if (_currentPageKey == "detail" && _currentDetailItem != null)
            {
                if (_pages.TryGetValue("detail", out var dp) && dp is DetailPage detailPage)
                {
                    entry.ScrollOffset = detailPage.GetScrollOffset();
                    entry.SourceTitle = detailPage.CurrentBreadcrumbSection;
                }
            }
            else if (_currentPageKey is "movies" or "books")
            {
                entry.SourceTitle = _currentPageKey == "movies" ? "ФИЛЬМЫ" : "КНИГИ";
                if (_pages.TryGetValue(_currentPageKey, out var lp) && lp is ListPage listPage)
                {
                    entry.ListState = listPage.GetCurrentState();
                    entry.ScrollOffset = entry.ListState.ScrollOffset;
                    entry.SearchQuery = entry.ListState.SearchText;
                }
            }
            else if (_currentPageKey == "home")
            {
                entry.SourceTitle = "ГЛАВНАЯ";
                if (_pages.TryGetValue("home", out var hp) && hp is HomePage homePage)
                {
                    entry.ScrollOffset = homePage.GetScrollOffset();
                }
            }
            else if (_currentPageKey == "favorites")
            {
                entry.SourceTitle = "ИЗБРАННОЕ";
                if (_pages.TryGetValue("favorites", out var fp) && fp is FavoritesPage favPage)
                {
                    entry.FavoritesState = favPage.GetCurrentState();
                    entry.ScrollOffset = entry.FavoritesState.ScrollOffset;
                }
            }
            else if (_currentPageKey == "settings")
            {
                entry.SourceTitle = "НАСТРОЙКИ";
            }

            return entry;
        }

        private void RestorePageState(UserControl page, NavigationEntry entry)
        {
            switch (page)
            {
                case ListPage lp:
                    if (entry.ListState != null)
                        lp.RestoreState(entry.ListState);
                    else if (!string.IsNullOrEmpty(entry.GenreFilter))
                        lp.SetGenreFilter(entry.GenreFilter!);
                    else if (!string.IsNullOrEmpty(entry.SearchQuery))
                        lp.SetSearchQuery(entry.SearchQuery!);
                    else
                        lp.Refresh();
                    break;
                case HomePage hp:
                    hp.UpdateCardsFavoriteState();
                    hp.RestoreScrollOffset(entry.ScrollOffset);
                    break;
                case FavoritesPage fp:
                    if (entry.FavoritesState != null)
                        fp.RestoreState(entry.FavoritesState);
                    else
                        fp.Refresh();
                    break;
                case SettingsPage sp:
                    sp.UpdateFavoritesCount();
                    break;
            }
        }

        public void ShowPage(string key, string? searchQuery = null, string? genreFilter = null, bool clearHistory = false, NavigationEntry? restoreEntry = null)
        {
            if (clearHistory)
            {
                _history.Clear();
                _currentDetailItem = null;
            }

            if (!_pages.ContainsKey(key))
                _pages[key] = CreatePage(key);

            var page = _pages[key];

            if (restoreEntry != null)
            {
                RestorePageState(page, restoreEntry);
            }
            else
            {
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
                    case "home":
                        ((HomePage)page).RefreshData();
                        break;
                    case "favorites":
                        ((FavoritesPage)page).Refresh();
                        break;
                    case "settings":
                        ((SettingsPage)page).UpdateFavoritesCount();
                        break;
                }
            }

            TransitionToPage(page, key);

            SidebarControl.SetActive(key);
            SidebarControl.UpdateFavoritesBadge(_data.Favorites.Count);
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
                    settingsPage.Initialize(_data, this, _settings);
                    return settingsPage;
                case "detail":
                    var detail = new DetailPage();
                    return detail;
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
                case HomePage hp: hp.RefreshData(); break;
                case DetailPage dp: dp.RefreshData(); break;
                case SettingsPage sp: sp.UpdateFavoritesCount(); break;
            }
        }

        /// <summary>
        /// Открывает полноценный экран подробной информации о произведении (DetailPage)
        /// с сохранением предыдущего экрана в истории навигации.
        /// </summary>
        public void OpenItem(MediaItem item)
        {
            if (item == null) return;

            var entry = CaptureCurrentState();
            _history.Push(entry);

            ShowDetailPage(item, fromSection: entry.SourceTitle);
        }

        /// <summary>
        /// Отображает детальный экран произведения на месте главной центральной области.
        /// </summary>
        public void ShowDetailPage(MediaItem item, bool isBackNavigation = false, double savedScrollOffset = 0, string fromSection = "")
        {
            bool isSamePage = _currentVisiblePage is DetailPage;
            _currentDetailItem = item;

            if (!_pages.TryGetValue("detail", out var page) || page is not DetailPage detailPage)
            {
                detailPage = new DetailPage();
                _pages["detail"] = detailPage;
            }

            detailPage.Initialize(item, _data, this, fromSection);
            detailPage.RestoreScrollOffset(savedScrollOffset);

            if (isSamePage)
            {
                var fadeIn = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(160));
                detailPage.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            else
            {
                TransitionToPage(detailPage, "detail");
            }

            // Подсвечиваем в боковом меню раздел (Фильмы или Книги)
            SidebarControl.SetActive(item.Kind == MediaKind.Movie ? "movies" : "books");

            // В правой панели переключаемся на «Ещё для вас», не дублируя текущее произведение
            DetailsPanelControl.ShowMoreForYou(item);
        }

        /// <summary>
        /// Возвращает на предыдущий шаг по истории навигации.
        /// </summary>
        public void NavigateBack()
        {
            if (_history.Count > 0)
            {
                var prev = _history.Pop();
                if (prev.PageKey == "detail" && prev.DetailItem != null)
                {
                    ShowDetailPage(prev.DetailItem, isBackNavigation: true, savedScrollOffset: prev.ScrollOffset, fromSection: prev.SourceTitle);
                }
                else
                {
                    _currentDetailItem = null;
                    ShowPage(prev.PageKey, searchQuery: prev.SearchQuery, genreFilter: prev.GenreFilter, clearHistory: false, restoreEntry: prev);
                    ShowFeaturedDefault();
                }
            }
            else
            {
                // Fallback если истории нет: возвращаемся в соответствующий каталог (Фильмы или Книги), а не всегда на главную!
                var fallbackKey = _currentDetailItem?.Kind == MediaKind.Book ? "books" : "movies";
                _currentDetailItem = null;
                ShowPage(fallbackKey, clearHistory: false);
                ShowFeaturedDefault();
            }
        }

        /// <summary>Вызывается при клике на ♡/♥ на любой карточке.</summary>
        public void OnFavoriteChanged(MediaItem? item) => OnFavoriteChanged(item, false);

        public void OnFavoriteChanged(MediaItem? item, bool skipFavoritesRefresh)
        {
            SidebarControl.UpdateFavoritesBadge(_data.Favorites.Count);

            if (_currentPageKey == "favorites")
            {
                if (!skipFavoritesRefresh)
                    RefreshCurrentPage();
            }
            else if (_currentPageKey == "home" && _pages.TryGetValue("home", out var page) && page is HomePage hp)
                hp.UpdateCardsFavoriteState();
            else if (_currentPageKey == "detail" && _pages.TryGetValue("detail", out var dPage) && dPage is DetailPage dp)
                dp.RefreshData();
            else if (_currentPageKey == "settings" && _pages.TryGetValue("settings", out var sPage) && sPage is SettingsPage sp)
                sp.UpdateFavoritesCount();

            if (_currentPageKey != "detail" && DetailsPanelControl.CurrentItem != null)
                DetailsPanelControl.ShowItem(DetailsPanelControl.CurrentItem);
            else if (_currentPageKey == "detail" && _currentDetailItem != null)
                DetailsPanelControl.ShowMoreForYou(_currentDetailItem);
        }

        /// <summary>
        /// Показывает аккуратное всплывающее Toast-уведомление внизу экрана
        /// длительностью 2 секунды без использования нативных MessageBox.
        /// </summary>
        public void ShowToast(string message, string icon = "✓")
        {
            ToastMessage.Text = message;
            ToastIcon.Text = icon;

            _toastTimer?.Stop();

            var easeOut = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = easeOut };
            var slideUp = new DoubleAnimation(10.0, 0.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = easeOut };

            ToastContainer.BeginAnimation(OpacityProperty, fadeIn);
            ToastTranslate.BeginAnimation(TranslateTransform.YProperty, slideUp);

            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.0) };
            _toastTimer.Tick += (_, _) =>
            {
                _toastTimer.Stop();
                var easeIn = new QuadraticEase { EasingMode = EasingMode.EaseIn };
                var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(220)) { EasingFunction = easeIn };
                ToastContainer.BeginAnimation(OpacityProperty, fadeOut);
            };
            _toastTimer.Start();
        }

        public void ApplyTheme(string theme, bool save = true)
        {
            Color accentColor, hoverColor, pressedColor;
            switch (theme)
            {
                case "Тёмная (Индиго)":
                    accentColor = Color.FromRgb(0x5B, 0x67, 0xA8);
                    hoverColor = Color.FromRgb(0x6F, 0x7C, 0xC2);
                    pressedColor = Color.FromRgb(0x48, 0x53, 0x8E);
                    break;
                case "Тёмная (Изумруд)":
                    accentColor = Color.FromRgb(0x4A, 0x8B, 0x6B);
                    hoverColor = Color.FromRgb(0x5B, 0xA0, 0x7E);
                    pressedColor = Color.FromRgb(0x3B, 0x74, 0x58);
                    break;
                default:
                    accentColor = Color.FromRgb(0xC1, 0x66, 0x6B);
                    hoverColor = Color.FromRgb(0xCD, 0x7A, 0x7F);
                    pressedColor = Color.FromRgb(0xA8, 0x55, 0x5A);
                    theme = "Тёмная (Тёплый нуар)";
                    break;
            }

            Application.Current.Resources["AccentBrush"] = new SolidColorBrush(accentColor);
            Application.Current.Resources["AccentHoverBrush"] = new SolidColorBrush(hoverColor);
            Application.Current.Resources["AccentPressedBrush"] = new SolidColorBrush(pressedColor);

            Color buttonTextColor = (theme == "Тёмная (Индиго)")
                ? Color.FromRgb(0xF2, 0xE9, 0xE4)
                : Color.FromRgb(0x16, 0x11, 0x14);
            Application.Current.Resources["AccentForegroundBrush"] = new SolidColorBrush(buttonTextColor);

            var gradient = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 1)
            };
            gradient.GradientStops.Add(new GradientStop(accentColor, 0));
            gradient.GradientStops.Add(new GradientStop(pressedColor, 1));
            Application.Current.Resources["AccentGradient"] = gradient;

            Application.Current.Resources["AccentGlow"] = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = accentColor,
                BlurRadius = 18,
                ShadowDepth = 0,
                Opacity = 0.55
            };

            SidebarControl?.SetActive(_currentPageKey);
            SidebarControl?.UpdateFavoritesBadge(_data.Favorites.Count);

            if (save)
            {
                _settings.Settings.Theme = theme;
                _settings.Save();
            }
        }

        public void ApplyScale(string scale, bool save = true)
        {
            double s = scale switch
            {
                "Компактный" or "Компактный (90%)" => 0.9,
                "Крупный" or "Крупный (110%)" => 1.1,
                _ => 1.0
            };
            AppScaleTransform.ScaleX = s;
            AppScaleTransform.ScaleY = s;

            if (save)
            {
                _settings.Settings.Scale = scale;
                _settings.Save();
            }
        }
    }
}
