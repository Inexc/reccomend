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
    /// <summary>Страница "Фильмы" или "Книги": поиск, фильтр жанра, сортировка, сетка карточек.</summary>
    public partial class ListPage : UserControl
    {
        private DataManager _data = null!;
        private MainWindow _app = null!;
        private Func<List<MediaItem>> _itemsGetter = null!;
        private bool _suppressFilterEvents;

        public ListPage()
        {
            InitializeComponent();
        }

        public void Initialize(DataManager data, MainWindow app, Func<List<MediaItem>> itemsGetter, string title, string icon)
        {
            _data = data;
            _app = app;
            _itemsGetter = itemsGetter;
            TitleText.Text = title;
            SubtitleText.Text = icon == "🎞"
                ? "Подборка фильмов по вашему вкусу"
                : "Истории, в которые стоит погрузиться";

            SearchPlaceholder.Text = icon == "🎞" ? "Поиск фильмов..." : "Поиск книг...";

            _suppressFilterEvents = true;
            var genres = new List<string> { "Все жанры" };
            genres.AddRange(_data.GenresFor(_itemsGetter()));
            GenreCombo.ItemsSource = genres;
            GenreCombo.SelectedIndex = 0;

            SortCombo.ItemsSource = new[] { "По рейтингу", "По названию", "Сначала новые" };
            SortCombo.SelectedIndex = 0;
            _suppressFilterEvents = false;

            UpdateResetButtonVisibility();
            Refresh();
        }

        public void SetSearchQuery(string query)
        {
            _suppressFilterEvents = true;
            SearchBox.Text = query;
            GenreCombo.SelectedIndex = 0;
            UpdateResetButtonVisibility();
            _suppressFilterEvents = false;
            Refresh();
        }

        /// <summary>Предустанавливает фильтр жанра (используется при клике на genre-чипс на главной).</summary>
        public void SetGenreFilter(string genre)
        {
            _suppressFilterEvents = true;
            SearchBox.Text = "";
            var items = GenreCombo.ItemsSource as List<string>;
            var index = items?.IndexOf(genre) ?? -1;
            GenreCombo.SelectedIndex = index >= 0 ? index : 0;
            UpdateResetButtonVisibility();
            _suppressFilterEvents = false;
            Refresh();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            UpdateResetButtonVisibility();
            if (!_suppressFilterEvents)
                Refresh();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var hasText = !string.IsNullOrEmpty(SearchBox.Text);
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

            UpdateResetButtonVisibility();
            if (!_suppressFilterEvents)
                Refresh();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            var accent = (System.Windows.Media.SolidColorBrush)Application.Current.Resources["AccentBrush"];
            SearchBorderBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty,
                new ColorAnimation(accent.Color, TimeSpan.FromMilliseconds(180)));
            SearchPlaceholder.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.35, TimeSpan.FromMilliseconds(180)));
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            var border2 = (System.Windows.Media.SolidColorBrush)Application.Current.Resources["BorderBrush2"];
            SearchBorderBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty,
                new ColorAnimation(border2.Color, TimeSpan.FromMilliseconds(180)));
            SearchPlaceholder.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.8, TimeSpan.FromMilliseconds(180)));
        }

        private void ClearSearchBtn_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = "";
            SearchBox.Focus();
        }

        private void ResetFiltersBtn_Click(object sender, RoutedEventArgs e)
        {
            _suppressFilterEvents = true;
            SearchBox.Text = "";
            GenreCombo.SelectedIndex = 0;
            SortCombo.SelectedIndex = 0;
            UpdateResetButtonVisibility();
            _suppressFilterEvents = false;
            Refresh();
        }

        private void UpdateResetButtonVisibility()
        {
            bool isModified = !string.IsNullOrWhiteSpace(SearchBox?.Text) ||
                              (GenreCombo != null && GenreCombo.SelectedIndex > 0) ||
                              (SortCombo != null && SortCombo.SelectedIndex > 0);
            if (ResetFiltersBtn != null)
                ResetFiltersBtn.Visibility = isModified ? Visibility.Visible : Visibility.Collapsed;
        }

        // ------------------------------------------------------------------
        // Адаптивная сетка: карточки равномерно заполняют доступную ширину
        // ------------------------------------------------------------------
        private const double CardGap = 16.0;

        private static int CalculateColumns(double width)
        {
            if (width >= 1150) return 5;
            if (width >= 750) return 4;
            if (width >= 520) return 3;
            return 2;
        }

        private void CardsScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveWidth(e.NewSize.Width);
        }

        private void ApplyResponsiveWidth(double availableWidth)
        {
            if (availableWidth <= 0 || CardsPanel.Children.Count == 0) return;

            var columns = CalculateColumns(availableWidth);
            var itemWidth = Math.Floor((availableWidth - (columns - 1) * CardGap - 2) / columns);

            for (int i = 0; i < CardsPanel.Children.Count; i++)
            {
                if (CardsPanel.Children[i] is CardControl card)
                {
                    card.Width = itemWidth;
                    card.Margin = ((i + 1) % columns == 0)
                        ? new Thickness(0, 0, 0, CardGap)
                        : new Thickness(0, 0, CardGap, CardGap);
                }
            }
        }

        public void Refresh()
        {
            var items = _itemsGetter();

            var query = SearchBox.Text;
            if (!string.IsNullOrWhiteSpace(query))
                items = _data.Search(query, items);

            var genre = GenreCombo.SelectedItem as string;
            if (!string.IsNullOrEmpty(genre) && genre != "Все жанры")
                items = items.Where(i => i.Genre.Contains(genre)).ToList();

            var sort = SortCombo.SelectedItem as string;
            items = sort switch
            {
                "По названию" => items.OrderBy(i => i.Title, StringComparer.CurrentCultureIgnoreCase).ToList(),
                "Сначала новые" => items.OrderByDescending(i => i.Year).ThenByDescending(i => i.Rating).ToList(),
                _ => items.OrderByDescending(i => i.Rating).ThenBy(i => i.Title).ToList()
            };

            ResultsCountText.Text = $"Найдено: {items.Count}";
            UpdateResetButtonVisibility();

            CardsPanel.Children.Clear();
            if (items.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                EmptyTranslate.Y = -8;
                EmptyState.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200)));
                EmptyTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-8.0, 0.0, TimeSpan.FromMilliseconds(200))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                });
                return;
            }

            EmptyState.Visibility = Visibility.Collapsed;

            var availableWidth = CardsScroll.ActualWidth;
            if (availableWidth <= 0) availableWidth = 800;

            var columns = CalculateColumns(availableWidth);
            var itemWidth = Math.Floor((availableWidth - (columns - 1) * CardGap - 2) / columns);

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var card = new CardControl
                {
                    Width = itemWidth,
                    Margin = ((i + 1) % columns == 0)
                        ? new Thickness(0, 0, 0, CardGap)
                        : new Thickness(0, 0, CardGap, CardGap)
                };
                card.Initialize(item, _data);
                card.OpenRequested += _app.OpenItem;
                card.FavoriteChanged += _app.OnFavoriteChanged;
                CardsPanel.Children.Add(card);
            }

            // Плавное обновление карточек (180ms)
            var anim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            CardsPanel.BeginAnimation(OpacityProperty, anim);
        }
    }
}
