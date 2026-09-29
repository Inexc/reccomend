using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using MovieBookRecommender.Models;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    /// <summary>Карточка произведения: постер во всю площадь, тёмный градиент
    /// снизу с названием/автором/рейтингом, сердечко избранного сверху справа.
    /// При наведении — плавное увеличение (scale) и мягкая тень.</summary>
    public partial class CardControl : UserControl
    {
        public MediaItem Item { get; private set; } = null!;
        private DataManager _data = null!;

        public event Action<MediaItem>? OpenRequested;
        public event Action<MediaItem>? FavoriteChanged;

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
            RatingText.Text = $"★ {item.Rating}";
            UpdateFavIcon();

            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, item.Poster);
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                PosterImage.Source = bmp;
            }
            catch (IOException)
            {
                // постер ещё не сгенерирован/скачан — оставляем пустым
            }
        }

        private static readonly Color RestColor = Color.FromArgb(0xCC, 0x16, 0x11, 0x14);
        private static readonly Color RestHoverColor = Color.FromArgb(0xE8, 0x16, 0x11, 0x14);
        private static readonly Color FavColor = Color.FromRgb(0xC1, 0x66, 0x6B);
        private static readonly Color FavHoverColor = Color.FromRgb(0xCD, 0x7A, 0x7F);

        private void UpdateFavIcon()
        {
            var isFav = _data.IsFavorite(Item.Id);
            FavIcon.Text = isFav ? "♥" : "♡";
            AnimateColor(HeartBrush, isFav ? FavColor : RestColor);
        }

        private void FavIcon_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            e.Handled = true;
            _data.ToggleFavorite(Item.Id);
            UpdateFavIcon();
            FavoriteChanged?.Invoke(Item);
        }

        private void RootBorder_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenRequested?.Invoke(Item);
        }

        private void RootBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            AnimateScale(1.02);
            AnimateImageZoom(1.06);
            AnimateOpacity(BrightenOverlay, 0.06);
            PosterFrame.Effect = (Effect)Application.Current.Resources["CardShadow"];
        }

        private void RootBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            AnimateScale(1.0);
            AnimateImageZoom(1.0);
            AnimateOpacity(BrightenOverlay, 0);
            PosterFrame.Effect = (Effect)Application.Current.Resources["CardRestShadow"];
        }

        private void HeartBadge_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var isFav = _data.IsFavorite(Item.Id);
            AnimateColor(HeartBrush, isFav ? FavHoverColor : RestHoverColor);
        }

        private void HeartBadge_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            UpdateFavIcon();
        }

        private void AnimateScale(double to)
        {
            var duration = TimeSpan.FromMilliseconds(140);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            CardScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, duration) { EasingFunction = ease });
            CardScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, duration) { EasingFunction = ease });
        }

        private void AnimateImageZoom(double to)
        {
            var duration = TimeSpan.FromMilliseconds(220);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            ImageScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(to, duration) { EasingFunction = ease });
            ImageScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(to, duration) { EasingFunction = ease });
        }

        private static void AnimateOpacity(UIElement element, double to)
        {
            var duration = TimeSpan.FromMilliseconds(140);
            element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(to, duration));
        }

        private static void AnimateColor(SolidColorBrush brush, Color to)
        {
            var duration = TimeSpan.FromMilliseconds(150);
            brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, duration));
        }
    }
}
