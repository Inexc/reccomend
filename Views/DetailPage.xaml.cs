using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MovieBookRecommender.Models;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    /// <summary>
    /// Полноценная страница подробной информации о фильме или книге.
    /// Включает большой кинематографичный Hero, полное описание с разворачиванием,
    /// блок деталей и фактов, и сетку похожих произведений.
    /// </summary>
    public partial class DetailPage : UserControl
    {
        private MediaItem _item = null!;
        private DataManager _data = null!;
        private MainWindow _app = null!;

        private bool _isDescExpanded = false;
        private double _currentCardWidth = 180;

        public DetailPage()
        {
            InitializeComponent();
        }

        public void Initialize(MediaItem item, DataManager data, MainWindow app)
        {
            _item = item;
            _data = data;
            _app = app;

            _isDescExpanded = false;

            PopulateData();
            BuildSimilarItems();
            AnimateIn();
        }

        public void RefreshData()
        {
            if (_item != null && _data != null)
            {
                UpdateFavoriteButton();
                UpdateFavoriteStatusText();
                BuildSimilarItems();
            }
        }

        private void AnimateIn()
        {
            Opacity = 0.0;
            PageTranslate.Y = 12.0;

            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };
            var slideUp = new DoubleAnimation(12.0, 0.0, TimeSpan.FromMilliseconds(200)) { EasingFunction = ease };

            BeginAnimation(OpacityProperty, fadeIn);
            PageTranslate.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }

        private void PopulateData()
        {
            // Хлебные крошки
            BreadcrumbSection.Text = _item.Kind == MediaKind.Movie ? "ФИЛЬМЫ" : "КНИГИ";
            BreadcrumbTitle.Text = _item.Title.ToUpperInvariant();

            // Hero Бэйдж типа и жанры
            HeroKindBadge.Text = _item.Kind == MediaKind.Movie ? "ФИЛЬМ" : "КНИГА";
            HeroGenresText.Text = _item.GenreText;

            // Название (крупно, до двух строк)
            HeroTitle.Text = _item.Title;

            // Рейтинг
            if (_item.Rating > 0)
            {
                HeroRatingGroup.Visibility = Visibility.Visible;
                HeroRatingText.Text = $"{_item.Rating:F1}";
            }
            else
            {
                HeroRatingGroup.Visibility = Visibility.Collapsed;
            }

            // Год
            if (_item.Year > 0)
            {
                HeroYearText.Visibility = Visibility.Visible;
                HeroYearText.Text = $"{_item.Year} г.";
            }
            else
            {
                HeroYearText.Visibility = Visibility.Collapsed;
            }

            // Создатель (Автор / Режиссёр)
            if (!string.IsNullOrWhiteSpace(_item.Author))
            {
                HeroAuthorText.Visibility = Visibility.Visible;
                var role = _item.Kind == MediaKind.Movie ? "Режиссёр" : "Автор";
                HeroAuthorText.Text = $"{role}: {_item.Author}";
            }
            else
            {
                HeroAuthorText.Visibility = Visibility.Collapsed;
            }

            // Разделительные точки в метаданных
            HeroMetaDot1.Visibility = (HeroRatingGroup.Visibility == Visibility.Visible && HeroYearText.Visibility == Visibility.Visible)
                ? Visibility.Visible : Visibility.Collapsed;

            HeroMetaDot2.Visibility = (HeroYearText.Visibility == Visibility.Visible && HeroAuthorText.Visibility == Visibility.Visible)
                ? Visibility.Visible : Visibility.Collapsed;

            // Описание
            if (string.IsNullOrWhiteSpace(_item.Description))
            {
                HeroDescription.Text = "Описание отсутствует.";
                ToggleDescBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                HeroDescription.Text = _item.Description;
                if (_item.Description.Length > 120)
                {
                    ToggleDescBtn.Visibility = Visibility.Visible;
                    ToggleDescBtn.Content = "Показать полностью ↓";
                    HeroDescription.MaxHeight = 68;
                }
                else
                {
                    ToggleDescBtn.Visibility = Visibility.Collapsed;
                    HeroDescription.MaxHeight = double.PositiveInfinity;
                }
            }

            // Кнопки
            PrimaryActionBtn.Content = _item.Kind == MediaKind.Movie ? "▷  Смотреть фильм" : "▷  Читать книгу";
            PrimaryActionBtn.Visibility = !string.IsNullOrWhiteSpace(_item.Title) ? Visibility.Visible : Visibility.Collapsed;

            UpdateFavoriteButton();

            // Постеры
            var posterImageSource = PosterService.GetImage(_item);
            HeroPosterImage.Source = posterImageSource;
            HeroBackdropImage.Source = posterImageSource;

            // Блок «Детали»
            DetailCreatorLabel.Text = _item.Kind == MediaKind.Movie ? "РЕЖИССЁР" : "АВТОР";
            DetailCreatorValue.Text = !string.IsNullOrWhiteSpace(_item.Author) ? _item.Author : "Не указан";

            DetailYearValue.Text = _item.Year > 0 ? $"{_item.Year} год" : "Не указан";
            DetailKindValue.Text = _item.Kind == MediaKind.Movie ? "Художественный фильм" : "Книга";

            DetailGenresValue.Text = !string.IsNullOrWhiteSpace(_item.GenreText) ? _item.GenreText : "Не указаны";
            DetailRatingValue.Text = _item.Rating > 0 ? $"★ {_item.Rating:F1} из 10" : "Без оценки";

            UpdateFavoriteStatusText();
        }

        private void UpdateFavoriteStatusText()
        {
            var isFav = _data.IsFavorite(_item.Id);
            DetailFavoriteStatus.Text = isFav ? "♥  В вашем избранном" : "Не добавлено";
            DetailFavoriteStatus.Foreground = isFav
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["TextMutedBrush"];
        }

        private void UpdateFavoriteButton()
        {
            var isFav = _data.IsFavorite(_item.Id);
            FavoriteBtn.Content = isFav ? "♥  В избранном" : "♡  В избранное";
        }

        private void ToggleDescBtn_Click(object sender, RoutedEventArgs e)
        {
            _isDescExpanded = !_isDescExpanded;
            if (_isDescExpanded)
            {
                HeroDescription.MaxHeight = double.PositiveInfinity;
                ToggleDescBtn.Content = "Свернуть ↑";
            }
            else
            {
                HeroDescription.MaxHeight = 68;
                ToggleDescBtn.Content = "Показать полностью ↓";
            }
        }

        private void PrimaryActionBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_item != null)
            {
                ExternalLinks.Open(_item);
            }
        }

        private void FavoriteBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_item == null) return;

            _data.ToggleFavorite(_item.Id);
            var isFav = _data.IsFavorite(_item.Id);

            UpdateFavoriteButton();
            UpdateFavoriteStatusText();

            _app.OnFavoriteChanged(_item);

            if (isFav)
                _app.ShowToast("Добавлено в избранное", "♥");
            else
                _app.ShowToast("Удалено из избранного", "♡");
        }

        private void BackBtn_Click(object sender, RoutedEventArgs e)
        {
            _app.NavigateBack();
        }

        // ------------------------------------------------------------------
        // Секция похожих произведений
        // ------------------------------------------------------------------
        private void BuildSimilarItems()
        {
            SimilarGrid.Children.Clear();

            var pool = _data.AllItems().Where(i => i.Id != _item.Id).ToList();
            var similar = RecommendationService.RecommendSimilar(_item, pool, limit: 4);

            var columns = SimilarGrid.Columns > 0 ? SimilarGrid.Columns : 4;
            var gap = 14.0;

            for (int i = 0; i < similar.Count; i++)
            {
                var candidate = similar[i];
                var card = new CardControl
                {
                    Width = _currentCardWidth,
                    Margin = ((i + 1) % columns == 0)
                        ? new Thickness(0, 0, 0, 16)
                        : new Thickness(0, 0, gap, 16)
                };
                card.Initialize(candidate, _data);

                // При клике на похожее — открываем его детальную страницу
                card.OpenRequested += it =>
                {
                    _app.OpenItem(it);
                };

                // При изменении избранного
                card.FavoriteChanged += _ =>
                {
                    UpdateFavoriteButton();
                    UpdateFavoriteStatusText();
                    _app.OnFavoriteChanged(candidate);
                };

                SimilarGrid.Children.Add(card);
            }
        }

        private void MainScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveWidth(Math.Max(300, e.NewSize.Width - 96));
        }

        private void ApplyResponsiveWidth(double availableWidth)
        {
            if (availableWidth <= 0 || SimilarGrid == null) return;

            int columns = availableWidth >= 680 ? 4 : 2;
            double cardGap = 14.0;
            double widthPerCard = Math.Floor((availableWidth - ((columns - 1) * cardGap)) / columns);
            widthPerCard = Math.Min(Math.Max(widthPerCard, 145), 245);
            _currentCardWidth = widthPerCard;

            SimilarGrid.Columns = columns;

            if (HeroPosterBorder != null)
            {
                if (availableWidth < 680)
                {
                    HeroPosterBorder.Width = 170;
                    HeroPosterBorder.Height = 250;
                }
                else
                {
                    HeroPosterBorder.Width = 216;
                    HeroPosterBorder.Height = 316;
                }
            }

            for (int i = 0; i < SimilarGrid.Children.Count; i++)
            {
                if (SimilarGrid.Children[i] is CardControl card)
                {
                    card.Width = widthPerCard;
                    card.Margin = ((i + 1) % columns == 0)
                        ? new Thickness(0, 0, 0, 16)
                        : new Thickness(0, 0, cardGap, 16);
                }
            }
        }
    }
}
