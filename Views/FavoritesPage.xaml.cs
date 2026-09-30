using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using MovieBookRecommender.Models;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    /// <summary>
    /// Страница «Избранное» — персональная коллекция пользователя.
    /// Включает числовую статистику с микроанимацией, инсайт вкусов,
    /// сегментированный переключатель Все/Фильмы/Книги, мгновенный поиск,
    /// сортировку, анимацию удаления карточек и блок персональных рекомендаций.
    /// </summary>
    public partial class FavoritesPage : UserControl
    {
        private DataManager _data = null!;
        private MainWindow _app = null!;

        private string _currentFilter = "all";
        private List<MediaItem> _currentDisplayedItems = new();
        private int _currentColumns = -1;
        private const double CardGap = 16.0;

        private readonly DispatcherTimer _searchDebounceTimer;

        public FavoritesPage()
        {
            InitializeComponent();
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            _searchDebounceTimer.Tick += (_, _) =>
            {
                _searchDebounceTimer.Stop();
                ApplyFilterAndSort();
            };
        }

        public void Initialize(DataManager data, MainWindow app)
        {
            _data = data;
            _app = app;

            UpdateFilterButtonStyles();
            Refresh();
        }

        public class FavoritesPageState
        {
            public string Filter { get; set; } = "all";
            public string SearchText { get; set; } = "";
            public int SortIndex { get; set; }
            public double ScrollOffset { get; set; }
        }

        public FavoritesPageState GetCurrentState()
        {
            return new FavoritesPageState
            {
                Filter = _currentFilter,
                SearchText = SearchBox?.Text ?? "",
                SortIndex = SortCombo?.SelectedIndex ?? 0,
                ScrollOffset = PageScroll?.VerticalOffset ?? 0
            };
        }

        public void RestoreState(FavoritesPageState state)
        {
            if (state == null) return;

            _currentFilter = state.Filter;
            UpdateFilterButtonStyles();

            if (SearchBox != null)
                SearchBox.Text = state.SearchText;

            if (SortCombo != null && state.SortIndex >= 0 && state.SortIndex < SortCombo.Items.Count)
                SortCombo.SelectedIndex = state.SortIndex;

            Refresh();

            if (state.ScrollOffset > 0)
            {
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
                {
                    PageScroll?.ScrollToVerticalOffset(state.ScrollOffset);
                }));
            }
        }

        public void ScrollToTop()
        {
            PageScroll?.ScrollToTop();
        }

        public void Refresh()
        {
            if (_data == null) return;

            UpdateStatisticsAndInsight();
            ApplyFilterAndSort();
            BuildRecommendations();
        }

        // ------------------------------------------------------------------
        // Статистика и инсайт предпочтений вкуса
        // ------------------------------------------------------------------
        private void UpdateStatisticsAndInsight()
        {
            var favItems = _data.GetFavoriteItems();
            var movieCount = favItems.Count(x => x.Kind == MediaKind.Movie);
            var bookCount = favItems.Count(x => x.Kind == MediaKind.Book);
            var totalCount = favItems.Count;

            AnimateNumberChange(TotalCountText, TotalScale, totalCount);
            AnimateNumberChange(MoviesCountText, MoviesScale, movieCount);
            AnimateNumberChange(BooksCountText, BooksScale, bookCount);

            // Инсайт вкуса на основе реального частотного анализа жанров
            if (totalCount == 0)
            {
                InsightBodyText.Text = "Добавляйте фильмы и книги в избранное, чтобы сформировать персональный профиль вкусов.";
            }
            else if (totalCount < 3)
            {
                InsightBodyText.Text = "Добавьте ещё несколько произведений, чтобы рекомендации стали точнее.";
            }
            else
            {
                var genreWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var it in favItems)
                {
                    foreach (var g in it.Genre)
                    {
                        genreWeights[g] = genreWeights.GetValueOrDefault(g, 0) + 1;
                    }
                }

                var topGenres = genreWeights
                    .OrderByDescending(kv => kv.Value)
                    .ThenBy(kv => kv.Key)
                    .Take(2)
                    .Select(kv => kv.Key)
                    .ToList();

                if (topGenres.Count > 0)
                {
                    var genresStr = string.Join(", ", topGenres);
                    InsightBodyText.Text = $"Похоже, вам нравится: {genresStr}. На основе этого мы формируем персональную подборку рекомендаций.";
                }
                else
                {
                    InsightBodyText.Text = "Ваша коллекция растёт! Рекомендации адаптируются под ваши предпочтения.";
                }
            }
        }

        private static void AnimateNumberChange(TextBlock textBlock, ScaleTransform scaleTransform, int newValue)
        {
            if (textBlock == null) return;
            var newStr = newValue.ToString();
            if (textBlock.Text == newStr) return;

            // Короткая микроанимация смены числа (100–140 ms)
            var fadeOut = new DoubleAnimation(1.0, 0.2, TimeSpan.FromMilliseconds(50));
            fadeOut.Completed += (_, _) =>
            {
                textBlock.Text = newStr;
                var fadeIn = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(70));
                textBlock.BeginAnimation(UIElement.OpacityProperty, fadeIn);

                var pop = new DoubleAnimation(1.15, 1.0, TimeSpan.FromMilliseconds(80))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pop);
                scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pop);
            };
            textBlock.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        // ------------------------------------------------------------------
        // Переключатель [ Все ] [ Фильмы ] [ Книги ]
        // ------------------------------------------------------------------
        private void FilterBtn_Click(object sender, RoutedEventArgs e)
        {
            if (sender == FilterAllBtn) _currentFilter = "all";
            else if (sender == FilterMoviesBtn) _currentFilter = "movies";
            else if (sender == FilterBooksBtn) _currentFilter = "books";

            UpdateFilterButtonStyles();
            ApplyFilterAndSort();
        }

        private void UpdateFilterButtonStyles()
        {
            var accentBrush = (Brush)Application.Current.Resources["AccentBrush"];
            var textMuted = (Brush)Application.Current.Resources["TextMutedBrush"];
            var transparent = Brushes.Transparent;

            SetSegmentState(FilterAllBtn, _currentFilter == "all", accentBrush, textMuted, transparent);
            SetSegmentState(FilterMoviesBtn, _currentFilter == "movies", accentBrush, textMuted, transparent);
            SetSegmentState(FilterBooksBtn, _currentFilter == "books", accentBrush, textMuted, transparent);
        }

        private static void SetSegmentState(Button btn, bool isActive, Brush accentBrush, Brush inactiveText, Brush transparent)
        {
            if (btn == null) return;
            btn.Tag = isActive ? "Active" : "Normal";
            btn.Background = isActive ? accentBrush : transparent;
            btn.Foreground = isActive ? Brushes.White : inactiveText;
            btn.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
        }

        // ------------------------------------------------------------------
        // Поиск в избранном
        // ------------------------------------------------------------------
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var hasText = !string.IsNullOrEmpty(SearchBox.Text);
            SearchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;

            if (ClearSearchBtn != null)
            {
                if (hasText)
                {
                    if (ClearSearchBtn.Visibility != Visibility.Visible)
                    {
                        ClearSearchBtn.Visibility = Visibility.Visible;
                        ClearSearchBtn.BeginAnimation(UIElement.OpacityProperty,
                            new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(150)));
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
            }

            _searchDebounceTimer.Stop();
            _searchDebounceTimer.Start();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var accent = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
            SearchBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(accent.Color, TimeSpan.FromMilliseconds(180)));
            SearchPlaceholder.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.35, TimeSpan.FromMilliseconds(180)));
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var borderSubtle = (SolidColorBrush)Application.Current.Resources["BorderSubtleBrush"];
            SearchBorderBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                new ColorAnimation(borderSubtle.Color, TimeSpan.FromMilliseconds(180)));
            SearchPlaceholder.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(0.8, TimeSpan.FromMilliseconds(180)));
        }

        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            SearchBox.Text = "";
            SearchBox.Focus();
            ApplyFilterAndSort();
        }

        // ------------------------------------------------------------------
        // Сортировка
        // ------------------------------------------------------------------
        private void SortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilterAndSort();
        }

        // ------------------------------------------------------------------
        // Основная фильтрация и заполнение сетки карточек
        // ------------------------------------------------------------------
        private void ApplyFilterAndSort()
        {
            if (_data == null || CardsPanel == null) return;

            var allFavs = _data.GetFavoriteItems();

            // 1. Фильтрация по типу
            var items = _currentFilter switch
            {
                "movies" => allFavs.Where(x => x.Kind == MediaKind.Movie).ToList(),
                "books" => allFavs.Where(x => x.Kind == MediaKind.Book).ToList(),
                _ => allFavs
            };

            // 2. Единый поиск в избранном через DataManager.Search
            var query = SearchBox?.Text;
            if (!string.IsNullOrWhiteSpace(query))
            {
                items = _data.Search(query, items);
            }

            // 3. Сортировка
            int sortIndex = SortCombo?.SelectedIndex ?? 0;
            items = sortIndex switch
            {
                1 => items.OrderByDescending(x => x.Rating).ToList(),
                2 => items.OrderBy(x => x.Title).ToList(),
                _ => items
            };

            _currentDisplayedItems = items;
            ResultsCountText.Text = $"Показано: {items.Count}";

            // Проверка Empty State
            if (items.Count == 0)
            {
                CardsPanel.Children.Clear();
                CardsPanel.Visibility = Visibility.Collapsed;
                ShowAppropriateEmptyState(allFavs.Count, query ?? "");
                return;
            }

            EmptyStateBorder.Visibility = Visibility.Collapsed;
            CardsPanel.Visibility = Visibility.Visible;
            PopulateCards(items);
        }

        private void PopulateCards(List<MediaItem> items)
        {
            CardsPanel.Children.Clear();

            var availableWidth = PageScroll.ActualWidth > 0 ? PageScroll.ActualWidth - 64 : 800;
            var columns = CalculateColumns(availableWidth);
            CardsPanel.Columns = columns;
            _currentColumns = columns;
            var halfGap = CardGap / 2.0;

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var card = new CardControl
                {
                    Width = double.NaN,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(halfGap, 0, halfGap, CardGap),
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = new ScaleTransform(1.0, 1.0)
                };
                card.Initialize(it, _data);

                // Открытие подробного экрана произведения
                card.OpenRequested += item => _app.OpenItem(item);

                // Плавное удаление из избранного
                card.FavoriteChanged += item => OnCardFavoriteToggled(card, item);

                CardsPanel.Children.Add(card);
            }

            // Плавное появление списка карточек
            var anim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            CardsPanel.BeginAnimation(OpacityProperty, anim);
        }

        // ------------------------------------------------------------------
        // Плавное удаление карточки из коллекции
        // ------------------------------------------------------------------
        private void OnCardFavoriteToggled(CardControl card, MediaItem item)
        {
            var isFav = _data.IsFavorite(item.Id);
            if (!isFav)
            {
                // Карточка удалена: запускаем плавное исчезновение (opacity 1 -> 0, scale 1 -> 0.95)
                var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(160));
                var scaleAnim = new DoubleAnimation(1.0, 0.95, TimeSpan.FromMilliseconds(160));

                fadeOut.Completed += (_, _) =>
                {
                    CardsPanel.Children.Remove(card);
                    _currentDisplayedItems.Remove(item);
                    ResultsCountText.Text = $"Показано: {_currentDisplayedItems.Count}";

                    UpdateStatisticsAndInsight();
                    BuildRecommendations();

                    if (_currentDisplayedItems.Count == 0)
                    {
                        var totalFavs = _data.GetFavoriteItems().Count;
                        ShowAppropriateEmptyState(totalFavs, SearchBox?.Text?.Trim() ?? "");
                    }

                    _app.OnFavoriteChanged(item);
                };

                card.BeginAnimation(UIElement.OpacityProperty, fadeOut);
                if (card.RenderTransform is ScaleTransform st)
                {
                    st.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                    st.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);
                }
            }
            else
            {
                // Если добавлена повторно
                Refresh();
                _app.OnFavoriteChanged(item);
            }
        }

        // ------------------------------------------------------------------
        // Настройка состояний Empty State
        // ------------------------------------------------------------------
        private void ShowAppropriateEmptyState(int totalFavCount, string query)
        {
            EmptyStateBorder.Visibility = Visibility.Visible;
            EmptyTranslate.Y = -8;

            if (totalFavCount == 0)
            {
                // Всё избранное полностью пустое
                EmptyStateIcon.Data = (Geometry)Application.Current.Resources["IconHeart"];
                EmptyStateTitle.Text = "Здесь пока пусто";
                EmptyStateSubtitle.Text = "Добавляйте фильмы и книги в избранное, чтобы собрать свою личную коллекцию и получать точные рекомендации.";

                EmptyGoMoviesBtn.Visibility = Visibility.Visible;
                EmptyGoMoviesBtn.Content = "Найти фильм";
                EmptyGoBooksBtn.Visibility = Visibility.Visible;
                EmptyGoBooksBtn.Content = "Найти книгу";
                EmptyResetSearchBtn.Visibility = Visibility.Collapsed;
            }
            else if (!string.IsNullOrWhiteSpace(query))
            {
                // Поиск ничего не нашёл
                EmptyStateIcon.Data = (Geometry)Application.Current.Resources["IconSearch"];
                EmptyStateTitle.Text = "Ничего не найдено";
                EmptyStateSubtitle.Text = "Попробуйте изменить поисковый запрос или выбрать другую категорию.";

                EmptyGoMoviesBtn.Visibility = Visibility.Collapsed;
                EmptyGoBooksBtn.Visibility = Visibility.Collapsed;
                EmptyResetSearchBtn.Visibility = Visibility.Visible;
            }
            else if (_currentFilter == "movies")
            {
                // Нет фильмов в избранном
                EmptyStateIcon.Data = (Geometry)Application.Current.Resources["IconFilm"];
                EmptyStateTitle.Text = "В избранном пока нет фильмов";
                EmptyStateSubtitle.Text = "Вы пока не сохранили ни одного фильма в свою личную коллекцию.";

                EmptyGoMoviesBtn.Visibility = Visibility.Visible;
                EmptyGoMoviesBtn.Content = "Перейти к фильмам";
                EmptyGoBooksBtn.Visibility = Visibility.Collapsed;
                EmptyResetSearchBtn.Visibility = Visibility.Collapsed;
            }
            else if (_currentFilter == "books")
            {
                // Нет книг в избранном
                EmptyStateIcon.Data = (Geometry)Application.Current.Resources["IconBook"];
                EmptyStateTitle.Text = "В избранном пока нет книг";
                EmptyStateSubtitle.Text = "Вы пока не сохранили ни одной книги в свою личную коллекцию.";

                EmptyGoMoviesBtn.Visibility = Visibility.Collapsed;
                EmptyGoBooksBtn.Visibility = Visibility.Visible;
                EmptyGoBooksBtn.Content = "Перейти к книгам";
                EmptyResetSearchBtn.Visibility = Visibility.Collapsed;
            }

            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
            var slide = new DoubleAnimation(-8.0, 0.0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            EmptyStateBorder.BeginAnimation(OpacityProperty, fadeIn);
            EmptyTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
        }

        private void GoToMovies_Click(object sender, RoutedEventArgs e) => _app.ShowPage("movies");
        private void GoToBooks_Click(object sender, RoutedEventArgs e) => _app.ShowPage("books");
        private void ResetSearch_Click(object sender, RoutedEventArgs e)
        {
            if (SearchBox != null) SearchBox.Text = "";
        }

        // ------------------------------------------------------------------
        // Персональные рекомендации внизу «На основе вашего избранного»
        // Выравнены с основной сеткой карточек
        // ------------------------------------------------------------------
        private void BuildRecommendations()
        {
            if (_data == null || RecsGrid == null) return;

            var favIds = _data.Favorites;
            if (favIds.Count == 0)
            {
                // Если избранного нет — скрываем блок рекомендаций
                RecommendationsSection.Visibility = Visibility.Collapsed;
                return;
            }

            RecommendationsSection.Visibility = Visibility.Visible;
            RecsGrid.Children.Clear();

            var availableWidth = PageScroll.ActualWidth > 0 ? PageScroll.ActualWidth - 64 : 800;
            var columns = CalculateColumns(availableWidth);
            RecsGrid.Columns = columns;
            var halfGap = CardGap / 2.0;

            // Персонализированный фид на основе жанров избранного, строго БЕЗ элементов из избранного
            var recs = RecommendationService.BuildPersonalizedFeed(_data.AllItems(), favIds, limit: columns);

            for (int i = 0; i < recs.Count; i++)
            {
                var it = recs[i];
                var card = new CardControl
                {
                    Width = double.NaN,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(halfGap, 0, halfGap, 16)
                };
                card.Initialize(it, _data);

                // Открытие подробностей
                card.OpenRequested += item => _app.OpenItem(item);

                // При добавлении рекомендации в избранное — обновляем избранное
                card.FavoriteChanged += _ =>
                {
                    Refresh();
                    _app.OnFavoriteChanged(it);
                };

                RecsGrid.Children.Add(card);
            }
        }

        // ------------------------------------------------------------------
        // Адаптивность при изменении размера окна
        // ------------------------------------------------------------------
        private static int CalculateColumns(double width)
        {
            if (width >= 1150) return 5;
            if (width >= 750) return 4;
            if (width >= 520) return 3;
            return 2;
        }

        private void PageScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveWidth(Math.Max(300, e.NewSize.Width - 64));
        }

        private void ApplyResponsiveWidth(double availableWidth)
        {
            if (availableWidth <= 0) return;

            var columns = CalculateColumns(availableWidth);
            if (columns == _currentColumns) return;
            _currentColumns = columns;

            if (CardsPanel != null)
                CardsPanel.Columns = columns;

            if (RecsGrid != null)
            {
                RecsGrid.Columns = columns;
                if (RecsGrid.Children.Count > 0 && RecsGrid.Children.Count != columns)
                {
                    BuildRecommendations();
                }
            }
        }
    }
}

