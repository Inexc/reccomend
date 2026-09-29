using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MovieBookRecommender.Models;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    /// <summary>Правая панель: показывает выбранный/рекомендованный объект,
    /// кнопку избранного, причины посмотреть/прочитать и похожие объекты.</summary>
    public partial class DetailsPanel : UserControl
    {
        private DataManager _data = null!;
        public MediaItem? CurrentItem { get; private set; }

        public event Action<MediaItem>? OpenRequested;
        public event Action? RefreshRequested;

        private static readonly string[] Reasons =
        {
            "Уникальный сюжет", "Глубокие эмоции", "Интересные персонажи"
        };

        public DetailsPanel()
        {
            InitializeComponent();
        }

        public void Initialize(DataManager data)
        {
            _data = data;
        }

        private void Refresh_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RefreshRequested?.Invoke();
        }

        public void ShowItem(MediaItem? item)
        {
            CurrentItem = item;
            ContentPanel.Children.Clear();

            if (item is null)
            {
                ContentPanel.Children.Add(new TextBlock
                {
                    Text = "Выберите фильм или книгу,\nчтобы увидеть подробности.",
                    Style = (Style)Application.Current.Resources["BodyText"],
                    Margin = new Thickness(0, 30, 0, 0)
                });
                return;
            }

            // Постер
            var posterBorder = new Border { CornerRadius = new CornerRadius(10), ClipToBounds = true, Height = 230, Margin = new Thickness(0, 0, 0, 10) };
            var image = new Image { Stretch = Stretch.UniformToFill };
            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, item.Poster);
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                image.Source = bmp;
            }
            catch (IOException) { }
            posterBorder.Child = image;
            ContentPanel.Children.Add(posterBorder);

            ContentPanel.Children.Add(new TextBlock
            {
                Text = item.Title, FontSize = 16, FontFamily = new FontFamily("Segoe UI Semibold"),
                Foreground = (Brush)Application.Current.Resources["TextBrush"], TextWrapping = TextWrapping.Wrap
            });

            ContentPanel.Children.Add(new TextBlock
            {
                Text = item.GenreText, Style = (Style)Application.Current.Resources["SmallText"],
                Margin = new Thickness(0, 3, 0, 3)
            });

            ContentPanel.Children.Add(new TextBlock
            {
                Text = $"⭐ {item.Rating}", FontWeight = FontWeights.SemiBold, FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["YellowBrush"],
                Margin = new Thickness(0, 0, 0, 6)
            });

            ContentPanel.Children.Add(new TextBlock
            {
                Text = item.Description, Style = (Style)Application.Current.Resources["SmallText"],
                TextWrapping = TextWrapping.Wrap, MaxHeight = 60, TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 10)
            });

            var watchButton = new Button
            {
                Style = (Style)Application.Current.Resources["AccentButton"],
                Content = item.Kind == MediaKind.Movie ? "▷  Смотреть" : "▷  Читать",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 10)
            };
            watchButton.Click += (_, _) => ExternalLinks.Open(item);
            ContentPanel.Children.Add(watchButton);

            var favButton = new Button
            {
                Style = (Style)Application.Current.Resources["GhostButton"],
                Content = FavText(item), HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 14)
            };
            favButton.Click += (_, _) =>
            {
                _data.ToggleFavorite(item.Id);
                favButton.Content = FavText(item);
            };
            ContentPanel.Children.Add(favButton);

            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Почему стоит посмотреть?", Style = (Style)Application.Current.Resources["H3"],
                Margin = new Thickness(0, 0, 0, 6)
            });
            foreach (var reason in Reasons)
            {
                ContentPanel.Children.Add(new TextBlock
                {
                    Text = $"✓  {reason}", Style = (Style)Application.Current.Resources["SmallText"],
                    Margin = new Thickness(0, 2, 0, 2)
                });
            }

            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Похожие", Style = (Style)Application.Current.Resources["H3"],
                Margin = new Thickness(0, 12, 0, 6)
            });

            var similarRow = new StackPanel { Orientation = Orientation.Horizontal };
            var pool = (item.Kind == MediaKind.Movie ? _data.Movies.Concat(_data.Books) : _data.Books.Concat(_data.Movies));
            var similar = RecommendationService.RecommendSimilar(item, pool, 3);
            foreach (var sim in similar)
            {
                var mini = new MiniCardControl { Margin = new Thickness(0, 0, 10, 0) };
                mini.Initialize(sim);
                mini.OpenRequested += m => OpenRequested?.Invoke(m);
                similarRow.Children.Add(mini);
            }
            ContentPanel.Children.Add(similarRow);
        }

        private string FavText(MediaItem item) =>
            _data.IsFavorite(item.Id) ? "♥  В избранном" : "♡  Добавить в избранное";
    }
}
