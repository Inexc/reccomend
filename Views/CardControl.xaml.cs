using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using MovieBookRecommender.Models;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    /// <summary>
    /// Универсальная интерактивная карточка произведения:
    /// - Аккуратный постер с фиксированным соотношением сторон (1 : 1.44);
    /// - Красивый monogram-fallback при отсутствии или загрузке изображения;
    /// - Название максимум в 2 строки без обрезания дескендеров;
    /// - Автор / Режиссёр;
    /// - Рейтинг ★ и Жанр;
    /// - Компактная кнопка избранного поверх постера (изолированный клик);
    /// - Четыре состояния: Hover, Pressed, Selected, Добавлено в избранное.
    /// </summary>
    public partial class CardControl : UserControl
    {
        public MediaItem Item { get; private set; } = null!;
        public bool IsSelected { get; private set; }

        private DataManager _data = null!;
        private bool _isHovered;
        private bool _isPressed;
        private double _lastWidth = -1;

        public event Action<MediaItem>? OpenRequested;
        public event Action<MediaItem>? FavoriteChanged;

        private static readonly Color RestColor = Color.FromArgb(0xDD, 0x16, 0x11, 0x14);
        private static readonly Color RestHoverColor = Color.FromArgb(0xF5, 0x22, 0x1A, 0x1E);
        private static Color FavColor => Application.Current?.Resources["AccentBrush"] is SolidColorBrush b ? b.Color : Color.FromRgb(0xC1, 0x66, 0x6B);
        private static Color FavHoverColor => Application.Current?.Resources["AccentHoverBrush"] is SolidColorBrush b ? b.Color : Color.FromRgb(0xCD, 0x7A, 0x7F);

        private static readonly Color BorderNormalColor = Color.FromRgb(0x2D, 0x23, 0x28);
        private static readonly Color BorderHoverColor = Color.FromRgb(0x56, 0x3F, 0x4E);
        private readonly SolidColorBrush _frameBorderBrush = new(BorderNormalColor);

        public CardControl()
        {
            InitializeComponent();
            PosterFrame.BorderBrush = _frameBorderBrush;
        }

        public void Initialize(MediaItem item, DataManager data)
        {
            Item = item;
            _data = data;

            TitleText.Text = item.Title;
            GenreText.Text = item.GenreText;
            RatingText.Text = $"{item.Rating:F1}";

            if (item.Kind == MediaKind.Movie)
            {
                SubtitleText.Text = !string.IsNullOrWhiteSpace(item.Author)
                    ? $"Реж. {item.Author}"
                    : (item.Year > 0 ? $"{item.Year} г." : "");
            }
            else
            {
                SubtitleText.Text = !string.IsNullOrWhiteSpace(item.Author)
                    ? item.Author
                    : (item.Year > 0 ? $"{item.Year} г." : "");
            }

            var img = PosterService.GetImage(item);
            if (img != null)
            {
                PosterImage.Source = img;
                PosterImage.Visibility = Visibility.Visible;
                FallbackContainer.Visibility = Visibility.Collapsed;
            }
            else
            {
                PosterImage.Source = null;
                PosterImage.Visibility = Visibility.Collapsed;
                FallbackContainer.Visibility = Visibility.Visible;
                FallbackKindText.Text = item.Kind == MediaKind.Movie ? "ФИЛЬМ" : "КНИГА";
                FallbackIcon.Data = item.Kind == MediaKind.Movie
                    ? (Geometry)Application.Current.Resources["IconFilm"]
                    : (Geometry)Application.Current.Resources["IconBook"];
            }

            UpdateFavIcon(animate: false);
        }

        private void CardControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width <= 0 || double.IsNaN(e.NewSize.Width)) return;
            if (Math.Abs(e.NewSize.Width - _lastWidth) < 2) return;
            _lastWidth = e.NewSize.Width;

            var targetHeight = Math.Round(e.NewSize.Width * 1.44);
            if (Math.Abs(PosterFrame.Height - targetHeight) > 2)
            {
                PosterFrame.Height = targetHeight;
                Height = targetHeight;
            }

            // Адаптация внутренних размеров для компактного отображения (например, в боковой панели)
            if (e.NewSize.Width < 145)
            {
                HeartBadge.Width = 24;
                HeartBadge.Height = 24;
                HeartBadge.CornerRadius = new CornerRadius(12);
                FavIcon.FontSize = 10.5;
                GenreText.FontSize = 9.5;
                RatingText.FontSize = 10;
                TitleText.FontSize = 11;
                TitleText.MaxHeight = 28;
                TitleText.LineHeight = 14;
                SubtitleText.FontSize = 9.5;
                BottomGradient.Height = 115;
            }
            else
            {
                HeartBadge.Width = 30;
                HeartBadge.Height = 30;
                HeartBadge.CornerRadius = new CornerRadius(15);
                FavIcon.FontSize = 13.5;
                GenreText.FontSize = 11;
                RatingText.FontSize = 11.5;
                TitleText.FontSize = 13.5;
                TitleText.MaxHeight = 38;
                TitleText.LineHeight = 18;
                SubtitleText.FontSize = 11;
                BottomGradient.Height = 148;
            }
        }

        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            SelectedBorder.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;

            if (selected)
            {
                var accentBrush = (SolidColorBrush)Application.Current.Resources["AccentBrush"];
                CardShadowEffect.Color = accentBrush.Color;
                CardShadowEffect.Opacity = 0.38;
            }
            else
            {
                CardShadowEffect.Color = Colors.Black;
                CardShadowEffect.Opacity = _isHovered ? 0.38 : 0.22;
            }
        }

        public void UpdateFavIcon(bool animate = false)
        {
            if (Item == null || _data == null) return;
            var isFav = _data.IsFavorite(Item.Id);
            FavIcon.Text = isFav ? "♥" : "♡";
            FavIcon.Foreground = isFav ? Brushes.White : (Brush)Application.Current.Resources["TextBrush"];

            if (animate)
            {
                AnimateColor(HeartBrush, isFav ? FavColor : RestColor);
            }
            else
            {
                HeartBrush.Color = isFav ? FavColor : RestColor;
            }

            HeartBadge.Opacity = isFav ? 1.0 : (_isHovered ? 1.0 : 0.80);
        }

        private void HeartBadge_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Блокируем всплытие нажатия, чтобы карточка не переходила в состояние Pressed и не открывалась
            e.Handled = true;
        }

        private void FavIcon_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (Item == null || _data == null) return;

            _data.ToggleFavorite(Item.Id);
            var isFav = _data.IsFavorite(Item.Id);
            UpdateFavIcon(animate: true);

            // Пульсирующая анимация (feedback) при нажатии на сердечко: 100% -> 120% -> 100%
            AnimateHeartPulse();

            var mainWin = Application.Current.MainWindow as MainWindow;
            if (isFav)
                mainWin?.ShowToast("Добавлено в избранное", "♥");
            else
                mainWin?.ShowToast("Удалено из избранного", "♡");

            FavoriteChanged?.Invoke(Item);
        }

        private void AnimateHeartPulse()
        {
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.20, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(90)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(180)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            HeartScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            HeartScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        private void RootBorder_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isPressed = true;
            AnimateScale(0.985, 60);
            AnimateTranslate(0.0, 60);
            AnimateShadow(0.28, 60);
        }

        private void RootBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_isPressed)
            {
                _isPressed = false;
                if (_isHovered)
                {
                    AnimateScale(1.015, 100);
                    AnimateTranslate(-2.5, 100);
                    AnimateShadow(0.38, 100);
                }
                else
                {
                    AnimateScale(1.0, 100);
                    AnimateTranslate(0.0, 100);
                    AnimateShadow(IsSelected ? 0.38 : 0.22, 100);
                }

                if (Item != null)
                    OpenRequested?.Invoke(Item);
            }
        }

        private void RootBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovered = true;
            AnimateScale(1.015, 120);
            AnimateTranslate(-2.5, 120);
            AnimateShadow(0.38, 120);
            AnimateColor(_frameBorderBrush, BorderHoverColor);

            HeartBadge.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120)));
        }

        private void RootBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _isHovered = false;
            _isPressed = false;
            AnimateScale(1.0, 120);
            AnimateTranslate(0.0, 120);
            AnimateShadow(IsSelected ? 0.38 : 0.22, 120);
            AnimateColor(_frameBorderBrush, BorderNormalColor);

            var isFav = _data?.IsFavorite(Item?.Id ?? "") ?? false;
            HeartBadge.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(isFav ? 1.0 : 0.80, TimeSpan.FromMilliseconds(120)));
        }

        private void HeartBadge_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var isFav = _data?.IsFavorite(Item?.Id ?? "") ?? false;
            AnimateColor(HeartBrush, isFav ? FavHoverColor : RestHoverColor);
        }

        private void HeartBadge_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            UpdateFavIcon(animate: true);
        }

        private void CardControl_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                if (Item != null)
                {
                    OpenRequested?.Invoke(Item);
                    e.Handled = true;
                }
            }
            else if (e.Key == System.Windows.Input.Key.Space)
            {
                FavIcon_MouseLeftButtonUp(HeartBadge, null!);
                e.Handled = true;
            }
        }

        private void CardControl_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            AnimateScale(1.015, 120);
            AnimateColor(_frameBorderBrush, BorderHoverColor);
            HeartBadge.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(120)));
        }

        private void CardControl_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (!_isHovered)
            {
                AnimateScale(1.0, 120);
                AnimateColor(_frameBorderBrush, BorderNormalColor);
                var isFav = _data?.IsFavorite(Item?.Id ?? "") ?? false;
                HeartBadge.BeginAnimation(UIElement.OpacityProperty,
                    new DoubleAnimation(isFav ? 1.0 : 0.80, TimeSpan.FromMilliseconds(120)));
            }
        }

        private void AnimateScale(double to, int durationMs = 120)
        {
            var duration = TimeSpan.FromMilliseconds(durationMs);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, duration) { EasingFunction = ease });
            CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, duration) { EasingFunction = ease });
        }

        private void AnimateTranslate(double toY, int durationMs = 120)
        {
            var duration = TimeSpan.FromMilliseconds(durationMs);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            CardTranslate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(toY, duration) { EasingFunction = ease });
        }

        private void AnimateShadow(double opacity, int durationMs = 120)
        {
            var duration = TimeSpan.FromMilliseconds(durationMs);
            CardShadowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(opacity, duration));
        }

        private static void AnimateColor(SolidColorBrush brush, Color to)
        {
            var duration = TimeSpan.FromMilliseconds(120);
            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, duration));
        }
    }
}

