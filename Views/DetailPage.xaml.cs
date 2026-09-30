using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MovieBookRecommender.Models;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    /// <summary>
    /// Полноценная насыщенная страница подробной информации о фильме или книге.
    /// Включает крупный кинематографичный Hero, структурированную строку метаданных,
    /// отдельный визуальный блок для описания, полноценный блок характеристик
    /// и адаптивную сетку похожих произведений во всю ширину экрана.
    /// </summary>
    public partial class DetailPage : UserControl
    {
        private MediaItem _item = null!;
        private DataManager _data = null!;
        private MainWindow _app = null!;

        private List<MediaItem> _similarCandidates = new();
        private int _currentColumns = -1;
        private const double CardGap = 16.0;

        public DetailPage()
        {
            InitializeComponent();
        }

        private string _fromSection = "";
        public string CurrentBreadcrumbSection => BreadcrumbSection?.Text ?? "";

        public double GetScrollOffset() => MainScroll?.VerticalOffset ?? 0;

        public void RestoreScrollOffset(double offset)
        {
            if (offset <= 0)
            {
                MainScroll?.ScrollToTop();
                return;
            }

            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
            {
                MainScroll?.ScrollToVerticalOffset(offset);
            }));
        }

        public void ScrollToTop()
        {
            MainScroll?.ScrollToTop();
        }

        public void Initialize(MediaItem item, DataManager data, MainWindow app, string fromSection = "")
        {
            _item = item;
            _data = data;
            _app = app;
            _fromSection = fromSection;

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
            PageTranslate.Y = 8.0;
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var slideUp = new DoubleAnimation(8.0, 0.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease };
            PageTranslate.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }

        private void PopulateData()
        {
            bool isMovie = _item.Kind == MediaKind.Movie;

            // 1. Хлебные крошки
            if (!string.IsNullOrWhiteSpace(_fromSection))
                BreadcrumbSection.Text = _fromSection.ToUpperInvariant();
            else
                BreadcrumbSection.Text = isMovie ? "ФИЛЬМЫ" : "КНИГИ";

            BreadcrumbTitle.Text = _item.Title.ToUpperInvariant();

            // 2. Бэйджи типа и категории в Hero
            HeroKindBadge.Text = isMovie ? "ФИЛЬМ" : "КНИГА";
            HeroCategoryBadge.Text = isMovie ? "Художественный фильм" : "Литературное издание";

            // 3. Название (главный визуальный элемент)
            HeroTitle.Text = _item.Title;

            // 4. Аккуратная строка метаданных в Hero
            if (_item.Rating > 0)
            {
                HeroRatingGroup.Visibility = Visibility.Visible;
                HeroRatingText.Text = $"{_item.Rating:F1}";
                DetailRatingValue.Text = $"★ {_item.Rating:F1} из 10";
                DetailRatingCategory.Text = _item.Rating >= 8.5
                    ? "Шедевр (высокая оценка)"
                    : (_item.Rating >= 7.5 ? "Высокая оценка" : "Положительные отзывы");
            }
            else
            {
                HeroRatingGroup.Visibility = Visibility.Collapsed;
                DetailRatingValue.Text = "Без оценки";
                DetailRatingCategory.Text = "Нет данных об оценках";
            }

            if (_item.Year > 0)
            {
                HeroYearText.Visibility = Visibility.Visible;
                HeroYearText.Text = $"{_item.Year} г.";
                DetailYearValue.Text = $"{_item.Year} год";
            }
            else
            {
                HeroYearText.Visibility = Visibility.Collapsed;
                DetailYearValue.Text = "Не указан";
            }

            if (!string.IsNullOrWhiteSpace(_item.Author))
            {
                HeroAuthorText.Visibility = Visibility.Visible;
                var role = isMovie ? "Режиссёр" : "Автор";
                HeroAuthorText.Text = $"{role}: {_item.Author}";
                DetailCreatorLabel.Text = isMovie ? "РЕЖИССЁР" : "АВТОР";
                DetailCreatorValue.Text = _item.Author;
            }
            else
            {
                HeroAuthorText.Visibility = Visibility.Collapsed;
                DetailCreatorLabel.Text = isMovie ? "РЕЖИССЁР" : "АВТОР";
                DetailCreatorValue.Text = "Не указан";
            }

            HeroGenresText.Text = !string.IsNullOrWhiteSpace(_item.GenreText) ? _item.GenreText : "";
            DetailGenresValue.Text = !string.IsNullOrWhiteSpace(_item.GenreText) ? _item.GenreText : "Не указаны";

            HeroMetaDot1.Visibility = (HeroYearText.Visibility == Visibility.Visible && HeroAuthorText.Visibility == Visibility.Visible)
                ? Visibility.Visible : Visibility.Collapsed;
            HeroMetaDot2.Visibility = (HeroAuthorText.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(_item.GenreText))
                ? Visibility.Visible : Visibility.Collapsed;

            // 5. Кнопки действий (заметные, одинаковой высоты)
            PrimaryActionBtn.Content = isMovie ? "▷  Смотреть фильм" : "▷  Читать книгу";
            PrimaryActionBtn.Visibility = !string.IsNullOrWhiteSpace(_item.Title) ? Visibility.Visible : Visibility.Collapsed;
            UpdateFavoriteButton();

            // 6. Постеры (крупный постер и фоновый бэкдроп)
            var posterImageSource = PosterService.GetImage(_item);
            HeroPosterImage.Source = posterImageSource;
            HeroBackdropImage.Source = posterImageSource;

            // 7. Отдельный блок описания и сюжета
            DescriptionHeaderTitle.Text = isMovie ? "О ФИЛЬМЕ" : "О КНИГЕ";
            FullStoryText.Text = !string.IsNullOrWhiteSpace(_item.Description)
                ? _item.Description
                : "Подробное описание для данного произведения пока отсутствует в каталоге.";

            // Интерактивные жанровые теги
            if (GenreChipsPanel != null && GenreChipsSection != null)
            {
                GenreChipsPanel.Children.Clear();
                if (_item.Genre != null && _item.Genre.Count > 0)
                {
                    GenreChipsSection.Visibility = Visibility.Visible;
                    foreach (var g in _item.Genre)
                    {
                        var chip = new Border
                        {
                            Background = (Brush)Application.Current.Resources["BgSoftBrush"],
                            BorderBrush = (Brush)Application.Current.Resources["BorderBrush2"],
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(6),
                            Padding = new Thickness(12, 5, 12, 5),
                            Margin = new Thickness(0, 0, 8, 8),
                            Cursor = Cursors.Hand
                        };
                        var tb = new TextBlock
                        {
                            Text = g,
                            FontSize = 12,
                            Foreground = (Brush)Application.Current.Resources["TextBrush"],
                            FontFamily = new FontFamily("Segoe UI Semibold")
                        };
                        chip.Child = tb;
                        chip.MouseEnter += (_, _) =>
                        {
                            chip.Background = (Brush)Application.Current.Resources["CardHoverBrush"];
                            chip.BorderBrush = (Brush)Application.Current.Resources["AccentBrush"];
                        };
                        chip.MouseLeave += (_, _) =>
                        {
                            chip.Background = (Brush)Application.Current.Resources["BgSoftBrush"];
                            chip.BorderBrush = (Brush)Application.Current.Resources["BorderBrush2"];
                        };
                        chip.MouseLeftButtonUp += (_, _) =>
                        {
                            _app.NavigateToGenreFromDetail(isMovie ? "movies" : "books", g);
                        };
                        GenreChipsPanel.Children.Add(chip);
                    }
                }
                else
                {
                    GenreChipsSection.Visibility = Visibility.Collapsed;
                }
            }

            // 8. Полноценный блок деталей и характеристик
            DetailYearLabel.Text = isMovie ? "ГОД ПРЕМЬЕРЫ" : "ГОД ИЗДАНИЯ";
            DetailRatingLabel.Text = isMovie ? "ОЦЕНКА ЗРИТЕЛЕЙ" : "ОЦЕНКА ЧИТАТЕЛЕЙ";
            DetailKindValue.Text = isMovie ? "Художественный фильм" : "Литературное издание";
            DetailAccessValue.Text = isMovie ? "Смотреть онлайн" : "Читать онлайн";

            UpdateFavoriteStatusText();
        }

        private void UpdateFavoriteStatusText()
        {
            var isFav = _data.IsFavorite(_item.Id);
            DetailFavoriteStatus.Text = isFav ? "♥  В избранном" : "Не добавлено";
            DetailFavoriteStatus.Foreground = isFav
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["TextMutedBrush"];
        }

        private void UpdateFavoriteButton()
        {
            var isFav = _data.IsFavorite(_item.Id);
            FavoriteBtn.Content = isFav ? "♥  В избранном" : "♡  В избранное";
            FavoriteBtn.BorderBrush = isFav
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["BorderBrush2"];
            FavoriteBtn.Foreground = isFav
                ? (Brush)Application.Current.Resources["AccentBrush"]
                : (Brush)Application.Current.Resources["TextBrush"];
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
        // Секция похожих произведений и адаптивность
        // ------------------------------------------------------------------
        private static int CalculateColumns(double width)
        {
            if (width >= 1150) return 5;
            if (width >= 750) return 4;
            if (width >= 520) return 3;
            return 2;
        }

        private void BuildSimilarItems()
        {
            _currentColumns = -1;
            var pool = _data.AllItems().Where(i => i.Id != _item.Id).ToList();
            _similarCandidates = RecommendationService.RecommendSimilar(_item, pool, limit: 12, favoriteIds: _data.Favorites);

            if (SimilarEmptyBanner != null && SimilarGrid != null)
            {
                var hasItems = _similarCandidates.Count > 0;
                SimilarEmptyBanner.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
                SimilarGrid.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            }

            var availableWidth = MainScroll?.ActualWidth > 0 ? MainScroll.ActualWidth - 64 : 800;
            ApplyResponsiveWidth(Math.Max(300, availableWidth));
        }

        private void MainScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveWidth(Math.Max(300, e.NewSize.Width - 64));
        }

        private void ApplyResponsiveWidth(double availableWidth)
        {
            if (availableWidth <= 0 || SimilarGrid == null) return;

            var columns = CalculateColumns(availableWidth);

            if (DetailsGrid != null)
            {
                DetailsGrid.Columns = availableWidth < 850 ? 2 : 4;
            }

            if (HeroPosterBorder != null)
            {
                if (availableWidth < 720)
                {
                    HeroPosterBorder.Width = 170;
                    HeroPosterBorder.Height = 246;
                }
                else
                {
                    HeroPosterBorder.Width = 220;
                    HeroPosterBorder.Height = 320;
                }
            }

            if (columns == _currentColumns) return;
            _currentColumns = columns;
            SimilarGrid.Columns = columns;

            RenderSimilarGrid(columns);
        }

        private void RenderSimilarGrid(int columns)
        {
            SimilarGrid.Children.Clear();
            var takeCount = Math.Min(_similarCandidates.Count, columns * 2);
            var itemsToShow = _similarCandidates.Take(takeCount).ToList();
            var halfGap = CardGap / 2.0;

            for (int i = 0; i < itemsToShow.Count; i++)
            {
                var candidate = itemsToShow[i];
                var card = new CardControl
                {
                    Width = double.NaN,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(halfGap, 0, halfGap, CardGap)
                };
                card.Initialize(candidate, _data);

                // При клике на похожее — открываем его детальную страницу
                card.OpenRequested += it => _app.OpenItem(it);

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
    }
}

