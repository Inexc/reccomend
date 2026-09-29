using System;
using System.IO;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using MovieBookRecommender.Models;

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
            TitleText.Text = item.Title.Length > 14 ? item.Title[..12] + "…" : item.Title;

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
            catch (IOException) { }
        }

        private void Root_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenRequested?.Invoke(Item);
        }
    }
}
