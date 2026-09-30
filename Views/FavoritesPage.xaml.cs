using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
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
        private double _currentCardWidth = 180;
        private const double CardGap = 14;

        public FavoritesPage()
        {
            InitializeComponent();
        }

        public void Initialize(DataManager data, MainWindow app)
        {
            _data = data;
            _app = app;

            UpdateFilterButtonStyles();
            Refresh();
        }

        public void Refresh()
        {
            if (_data == null) return;

            UpdateStatisticsAndInsight();
            ApplyFilterAndSort();
            BuildRecommendations();
        }

        // ------------------------------------------------------------------
        // Статистика и инсайт предпочтений (Разделы 2, 14, 15)
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

            // Инсайт вкуса на основе реального частотного анализа жанров (Раздел 15)
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
                    InsightBodyText.Text = $"Похоже, вам нравится: {genresStr}. На основе этого мы формируем подборку рекомендаций.";
                }
                else
                {
                    InsightBodyText.Text = "Ваша коллекция растёт! Рекомендации адаптируются под ваши оценки.";
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
        // Переключатель [ Все ] [ Фильмы ] [ Книги ] (Раздел 3)
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
            var textBrush = (Brush)Application.Current.Resources["TextBrush"];
            var textMuted = (Brush)Application.Current.Resources["TextMutedBrush"];
            var transparent = Brushes.Transparent;

            SetSegmentState(FilterAllBtn, _currentFilter == "all", accentBrush, textBrush, textMuted, transparent);
            SetSegmentState(FilterMoviesBtn, _currentFilter == "movies", accentBrush, textBrush, textMuted, transparent);
            SetSegmentState(FilterBooksBtn, _currentFilter == "books", accentBrush, textBrush, textMuted, transparent);
        }

        private static void SetSegmentState(Button btn, bool isActive, Brush accentBrush, Brush activeText, Brush inactiveText, Brush transparent)
        {
            if (btn == null) return;
            btn.Background = isActive ? accentBrush : transparent;
            btn.Foreground = isActive ? Brushes.White : inactiveText;
            btn.BorderThickness = new Thickness(0);
            btn.FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal;
        }

        // ------------------------------------------------------------------
        // Поиск в избранном (Раздел 4)
        // ------------------------------------------------------------------
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var hasText = !string.IsNullOrEmpty(SearchBox.Text);
            SearchPlaceholder.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;

            if (ClearSearchBtn != null)
            {
                ClearSearchBtn.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;
                ClearSearchBtn.Opacity = hasText ? 1.0 : 0.0;
            }

            ApplyFilterAndSort();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            SearchBoxContainer.BorderBrush = (Brush)Application.Current.Resources["AccentBrush"];
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SearchBoxContainer.BorderBrush = (Brush)Application.Current.Resources["BorderSubtleBrush"];
        }

        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        // ------------------------------------------------------------------
        // Сортировка (Раздел 5)
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

            // 2. Поиск в избранном (по названию, автору/режиссёру, жанру)
            var query = SearchBox?.Text?.Trim().ToLowerInvariant() ?? "";
            if (!string.IsNullOrWhiteSpace(query))
            {
                items = items.Where(it =>
                {
                    var haystack = $"{it.Title} {it.Author} {it.GenreText}".ToLowerInvariant();
                    return haystack.Contains(query);
                }).ToList();
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
                ShowAppropriateEmptyState(allFavs.Count, query);
                return;
            }

            EmptyStateBorder.Visibility = Visibility.Collapsed;
            CardsPanel.Visibility = Visibility.Visible;
            PopulateCards(items);
        }

        private void PopulateCards(List<MediaItem> items)
        {
            CardsPanel.Children.Clear();

            var availableWidth = PageScroll.ActualWidth - 96;
            if (availableWidth <= 0) availableWidth = 800;

            int columns = availableWidth >= 680 ? 4 : 2;
            double widthPerCard = Math.Floor((availableWidth - ((columns - 1) * CardGap)) / columns);
            widthPerCard = Math.Min(Math.Max(widthPerCard, 145), 240);
            _currentCardWidth = widthPerCard;

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                var card = new CardControl
                {
                    Width = widthPerCard,
                    Margin = new Thickness(0, 0, CardGap, 16),
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
        // Плавное удаление карточки (Раздел 6 & 7)
        // ------------------------------------------------------------------
        private void OnCardFavoriteToggled(CardControl card, MediaItem item)
        {
            var isFav = _data.IsFavorite(item.Id);
            if (!isFav)
            {
                // Карточка удалена: запускаем плавное исчезновение (opacity 1 -> 0, scale 1 -> 0.96)
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
        // Настройка Empty States (Разделы 8 & 9)
        // ------------------------------------------------------------------
        private void ShowAppropriateEmptyState(int totalFavCount, string query)
        {
            EmptyStateBorder.Visibility = Visibility.Visible;
            EmptyTranslate.Y = -8;

            if (totalFavCount == 0)
            {
                // Всё избранное полностью пустое (Раздел 8)
                EmptyStateIcon.Data = (Geometry)Application.Current.Resources["IconHeart"];
                EmptyStateTitle.Text = "Здесь пока пусто";
                EmptyStateSubtitle.Text = "Добавляйте фильмы и книги в избранное, чтобы собрать свою коллекцию.";

                EmptyGoMoviesBtn.Visibility = Visibility.Visible;
                EmptyGoMoviesBtn.Content = "Найти фильм";
                EmptyGoBooksBtn.Visibility = Visibility.Visible;
                EmptyGoBooksBtn.Content = "Найти книгу";
                EmptyResetSearchBtn.Visibility = Visibility.Collapsed;
            }
            else if (!string.IsNullOrWhiteSpace(query))
            {
                // Поиск ничего не нашёл (Раздел 9)
                EmptyStateIcon.Data = (Geometry)Application.Current.Resources["IconSearch"];
                EmptyStateTitle.Text = "Ничего не найдено";
                EmptyStateSubtitle.Text = "Попробуйте изменить поисковый запрос или выбрать другой тип.";

                EmptyGoMoviesBtn.Visibility = Visibility.Collapsed;
                EmptyGoBooksBtn.Visibility = Visibility.Collapsed;
                EmptyResetSearchBtn.Visibility = Visibility.Visible;
            }
            else if (_currentFilter == "movies")
            {
                // Нет фильмов в избранном (Раздел 9)
                EmptyStateIcon.Data = (Geometry)Application.Current.Resources["IconFilm"];
                EmptyStateTitle.Text = "В избранном пока нет фильмов";
                EmptyStateSubtitle.Text = "Вы пока не сохранили ни одного фильма в свою коллекцию.";

                EmptyGoMoviesBtn.Visibility = Visibility.Visible;
                EmptyGoMoviesBtn.Content = "Перейти к фильмам";
                EmptyGoBooksBtn.Visibility = Visibility.Collapsed;
                EmptyResetSearchBtn.Visibility = Visibility.Collapsed;
            }
            else if (_currentFilter == "books")
            {
                // Нет книг в избранном (Раздел 9)
                EmptyStateIcon.Data = (Geometry)Application.Current.Resources["IconBook"];
                EmptyStateTitle.Text = "В избранном пока нет книг";
                EmptyStateSubtitle.Text = "Вы пока не сохранили ни одной книги в свою коллекцию.";

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
        // (Разделы 10 & 11) - Исключение дубликатов гарантировано!
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

            // Персонализированный фид на основе жанров избранного, строго БЕЗ элементов из избранного
            var recs = RecommendationService.BuildPersonalizedFeed(_data.AllItems(), favIds, limit: 4);

            var availableWidth = PageScroll.ActualWidth - 96;
            if (availableWidth <= 0) availableWidth = 800;

            int columns = availableWidth >= 680 ? 4 : 2;
            double widthPerCard = Math.Floor((availableWidth - ((columns - 1) * CardGap)) / columns);
            widthPerCard = Math.Min(Math.Max(widthPerCard, 145), 240);

            RecsGrid.Columns = columns;

            for (int i = 0; i < recs.Count; i++)
            {
                var it = recs[i];
                var card = new CardControl
                {
                    Width = widthPerCard,
                    Margin = ((i + 1) % columns == 0)
                        ? new Thickness(0, 0, 0, 16)
                        : new Thickness(0, 0, CardGap, 16)
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
        // Адаптивность при изменении размера окна (Раздел 17)
        // ------------------------------------------------------------------
        private void PageScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveWidth(Math.Max(300, e.NewSize.Width - 96));
        }

        private void ApplyResponsiveWidth(double availableWidth)
        {
            if (availableWidth <= 0) return;

            int columns = availableWidth >= 680 ? 4 : 2;
            double widthPerCard = Math.Floor((availableWidth - ((columns - 1) * CardGap)) / columns);
            widthPerCard = Math.Min(Math.Max(widthPerCard, 145), 240);
            _currentCardWidth = widthPerCard;

            if (RecsGrid != null)
            {
                RecsGrid.Columns = columns;
                for (int i = 0; i < RecsGrid.Children.Count; i++)
                {
                    if (RecsGrid.Children[i] is CardControl card)
                    {
                        card.Width = widthPerCard;
                        card.Margin = ((i + 1) % columns == 0)
                            ? new Thickness(0, 0, 0, 16)
                            : new Thickness(0, 0, CardGap, 16);
                    }
                }
            }

            if (CardsPanel != null)
            {
                foreach (var child in CardsPanel.Children)
                {
                    if (child is CardControl card)
                    {
                        card.Width = widthPerCard;
                    }
                }
            }
        }
    }
}

