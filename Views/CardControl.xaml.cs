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
    /// Карточка произведения: постер во всю площадь, тёмный градиент
    /// снизу с названием/автором/рейтингом, сердечко избранного сверху справа.
    /// При наведении — плавное увеличение (2.5%), приближение картинки,
    /// мягкая тень и подсветка кнопки избранного.
    /// </summary>
    public partial class CardControl : UserControl
    {
        public MediaItem Item { get; private set; } = null!;
        private DataManager _data = null!;

        public event Action<MediaItem>? OpenRequested;
        public event Action<MediaItem>? FavoriteChanged;

        private static readonly Color RestColor = Color.FromArgb(0xDD, 0x16, 0x11, 0x14);
        private static readonly Color RestHoverColor = Color.FromArgb(0xF5, 0x22, 0x1A, 0x1E);
        private static readonly Color FavColor = Color.FromRgb(0xC1, 0x66, 0x6B);
        private static readonly Color FavHoverColor = Color.FromRgb(0xCD, 0x7A, 0x7F);

        public CardControl()
        {
            InitializeComponent();
        }

        public void Initialize(MediaItem item, DataManager data)
        {
            Item = item;
            _data = data;

            TitleText.Text = item.Title;
            SubtitleText.Text = string.IsNullOrWhiteSpace(item.Author) ? item.GenreText : item.Author;
            RatingText.Text = $"★ {item.Rating:F1}";
            UpdateFavIcon(animate: false);

            PosterImage.Source = PosterService.GetImage(item);
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

            HeartBadge.Opacity = isFav ? 1.0 : 0.75;
        }

        private void FavIcon_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            if (Item == null || _data == null) return;

            _data.ToggleFavorite(Item.Id);
            var isFav = _data.IsFavorite(Item.Id);
            UpdateFavIcon(animate: true);

            // Пульсирующая анимация (feedback) при нажатии на сердечко
            AnimateHeartPulse();

            // Показываем ненавязчивое уведомление внизу экрана
            var mainWin = Application.Current.MainWindow as MainWindow;
            if (isFav)
                mainWin?.ShowToast($"«{Item.Title}» добавлено в избранное", "♥");
            else
                mainWin?.ShowToast($"«{Item.Title}» удалено из избранного", "♡");

            FavoriteChanged?.Invoke(Item);
        }

        private void AnimateHeartPulse()
        {
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.35, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(110)))
            {
                EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.5 }
            });
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(220)))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });

            HeartScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            HeartScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        private void RootBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Item != null)
                OpenRequested?.Invoke(Item);
        }

        private void RootBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            AnimateScale(1.025);
            AnimateImageZoom(1.05);
            AnimateOpacity(BrightenOverlay, 0.05);
            AnimateShadow(opacity: 0.48, blur: 20);

            // Кнопка избранного становится более заметной
            HeartBadge.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(180)));
        }

        private void RootBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            AnimateScale(1.0);
            AnimateImageZoom(1.0);
            AnimateOpacity(BrightenOverlay, 0);
            AnimateShadow(opacity: 0.25, blur: 10);

            var isFav = _data?.IsFavorite(Item?.Id ?? "") ?? false;
            HeartBadge.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(isFav ? 1.0 : 0.75, TimeSpan.FromMilliseconds(180)));
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

        private void AnimateScale(double to)
        {
            var duration = TimeSpan.FromMilliseconds(180);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, duration) { EasingFunction = ease });
            CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, duration) { EasingFunction = ease });
        }

        private void AnimateImageZoom(double to)
        {
            var duration = TimeSpan.FromMilliseconds(200);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            ImageScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, duration) { EasingFunction = ease });
            ImageScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, duration) { EasingFunction = ease });
        }

        private void AnimateShadow(double opacity, double blur)
        {
            var duration = TimeSpan.FromMilliseconds(180);
            CardShadowEffect.BeginAnimation(DropShadowEffect.OpacityProperty, new DoubleAnimation(opacity, duration));
            CardShadowEffect.BeginAnimation(DropShadowEffect.BlurRadiusProperty, new DoubleAnimation(blur, duration));
        }

        private static void AnimateOpacity(UIElement element, double to)
        {
            var duration = TimeSpan.FromMilliseconds(180);
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(to, duration));
        }

        private static void AnimateColor(SolidColorBrush brush, Color to)
        {
            var duration = TimeSpan.FromMilliseconds(150);
            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, duration));
        }
    }
}

