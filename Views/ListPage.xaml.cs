using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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

            _suppressFilterEvents = true;
            var genres = new List<string> { "Все" };
            genres.AddRange(_data.GenresFor(_itemsGetter()));
            GenreCombo.ItemsSource = genres;
            GenreCombo.SelectedIndex = 0;

            SortCombo.ItemsSource = new[] { "По рейтингу", "По названию" };
            SortCombo.SelectedIndex = 0;
            _suppressFilterEvents = false;

            Refresh();
        }

        public void SetSearchQuery(string query)
        {
            _suppressFilterEvents = true;
            SearchBox.Text = query;
            GenreCombo.SelectedIndex = 0;
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
            _suppressFilterEvents = false;
            Refresh();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_suppressFilterEvents)
                Refresh();
        }

        // ------------------------------------------------------------------
        // Адаптивная сетка: карточки равномерно заполняют доступную ширину
        // ------------------------------------------------------------------
        private const double CardGap = 14;
        private const double MinCardWidth = 172;
        private const double MaxCardWidth = 240;

        private void CardsScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveWidth(e.NewSize.Width);
        }

        private void ApplyResponsiveWidth(double availableWidth)
        {
            if (availableWidth <= 0) return;

            var columns = Math.Max(1, (int)((availableWidth + CardGap) / (MinCardWidth + CardGap)));
            var itemWidth = availableWidth / columns - CardGap;
            itemWidth = Math.Min(itemWidth, MaxCardWidth);

            foreach (var child in CardsPanel.Children)
            {
                if (child is CardControl card)
                    card.Width = itemWidth;
            }
        }

        public void Refresh()
        {
            var items = _itemsGetter();

            var query = SearchBox.Text;
            if (!string.IsNullOrWhiteSpace(query))
                items = _data.Search(query, items);

            var genre = GenreCombo.SelectedItem as string;
            if (!string.IsNullOrEmpty(genre) && genre != "Все")
                items = items.Where(i => i.Genre.Contains(genre)).ToList();

            items = (SortCombo.SelectedItem as string) == "По названию"
                ? items.OrderBy(i => i.Title).ToList()
                : items.OrderByDescending(i => i.Rating).ToList();

            CardsPanel.Children.Clear();
            if (items.Count == 0)
            {
                CardsPanel.Children.Add(new TextBlock
                {
                    Text = "Ничего не найдено", Style = (Style)Application.Current.Resources["BodyText"],
                    Margin = new Thickness(20, 40, 0, 0)
                });
                return;
            }

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
                card.FavoriteChanged += _app.OnFavoriteChanged;
                CardsPanel.Children.Add(card);
            }
        }
    }
}
