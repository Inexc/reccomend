using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using MovieBookRecommender.Models;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    public partial class MiniCardControl : UserControl
    {
        public MediaItem Item { get; private set; } = null!;
        public event Action<MediaItem>? OpenRequested;

        public MiniCardControl()
        {
            InitializeComponent();
        }

        public void Initialize(MediaItem item)
        {
            Item = item;
            TitleText.Text = item.Title.Length > 13 ? item.Title[..11] + "…" : item.Title;
            PosterImage.Source = PosterService.GetImage(item);
        }

        private void Root_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (Item != null)
                OpenRequested?.Invoke(Item);
        }

        private void Root_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var anim = new DoubleAnimation(1.05, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            MiniScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            MiniScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            PosterBorder.BorderBrush = (Brush)Application.Current.Resources["AccentBrush"];
        }

        private void Root_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            var anim = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(160))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            MiniScale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            MiniScale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
            PosterBorder.BorderBrush = (Brush)Application.Current.Resources["BorderBrush2"];
        }
    }
}

