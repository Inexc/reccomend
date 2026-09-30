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
    /// Правая панель: показывает выбранный/рекомендованный объект,
    /// автора/режиссёра, кнопку избранного, причины посмотреть/прочитать
    /// и динамически подобранные похожие объекты.
    /// </summary>
    public partial class DetailsPanel : UserControl
    {
        private DataManager _data = null!;
        public MediaItem? CurrentItem { get; private set; }

        public event Action<MediaItem>? OpenRequested;
        public event Action? RefreshRequested;
        public event Action<MediaItem>? FavoriteChanged;

        private static readonly string[] MovieReasons =
        {
            "Уникальная режиссура и визуал", "Захватывающий сюжет", "Глубокие эмоции и игра актёров"
        };

        private static readonly string[] BookReasons =
        {
            "Увлекательный авторский слог", "Продуманный мир и персонажи", "Оставляет сильное послевкусие"
        };

        public DetailsPanel()
        {
            InitializeComponent();
        }

        public void Initialize(DataManager data)
        {
            _data = data;
        }

        private bool _isMoreMode = false;
        private MediaItem? _currentItemForMore;
        private int _moreSeedOffset = 0;

        private void Refresh_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            TriggerRefresh();
        }

        private void Refresh_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter || e.Key == System.Windows.Input.Key.Space)
            {
                TriggerRefresh();
                e.Handled = true;
            }
        }

        private void TriggerRefresh()
        {
            if (_isMoreMode && _currentItemForMore != null)
            {
                ShowMoreForYou(_currentItemForMore, shuffle: true);
                return;
            }
            RefreshRequested?.Invoke();
        }

        private void Refresh_GotKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (RefreshButton != null)
                RefreshButton.Background = new SolidColorBrush(Color.FromArgb(0x4D, 0x2A, 0x20, 0x24));
        }

        private void Refresh_LostKeyboardFocus(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e)
        {
            if (RefreshButton != null)
                RefreshButton.Background = Brushes.Transparent;
        }

        public void ShowItem(MediaItem? item)
        {
            _isMoreMode = false;
            _currentItemForMore = null;
            if (HeaderTitle != null) HeaderTitle.Text = "ПОДБОР ДЛЯ ВАС";
            CurrentItem = item;

            if (ContentPanel.Children.Count == 0)
            {
                PopulateItem(item);
                AnimateIn();
                return;
            }

            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(70));
            fadeOut.Completed += (_, _) =>
            {
                PopulateItem(item);
                AnimateIn();
            };
            ContentPanel.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void AnimateIn()
        {
            ContentTranslate.Y = 10;
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(130)) { EasingFunction = ease };
            var slideUp = new DoubleAnimation(10.0, 0.0, TimeSpan.FromMilliseconds(130)) { EasingFunction = ease };
            ContentPanel.BeginAnimation(OpacityProperty, fadeIn);
            ContentTranslate.BeginAnimation(TranslateTransform.YProperty, slideUp);
        }

        private void PopulateItem(MediaItem? item)
        {
            ContentPanel.Children.Clear();

            if (item is null)
            {
                ContentPanel.Children.Add(new TextBlock
                {
                    Text = "Выберите фильм или книгу,\nчтобы увидеть подробности.",
                    Style = (Style)Application.Current.Resources["BodyText"],
                    Margin = new Thickness(0, 30, 0, 0),
                    TextAlignment = TextAlignment.Center
                });
                return;
            }

            // Постер (компактный и аккуратный: 185px)
            var posterBorder = new Border
            {
                CornerRadius = new CornerRadius(8),
                ClipToBounds = true,
                Height = 185,
                Margin = new Thickness(0, 0, 0, 10),
                BorderBrush = (Brush)Application.Current.Resources["BorderBrush2"],
                BorderThickness = new Thickness(1),
                Background = (Brush)Application.Current.Resources["CardBrush"]
            };
            var image = new Image
            {
                Stretch = Stretch.UniformToFill,
                Source = PosterService.GetImage(item)
            };
            posterBorder.Child = image;
            ContentPanel.Children.Add(posterBorder);

            // Название
            ContentPanel.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 15,
                FontFamily = new FontFamily("Segoe UI Semibold"),
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 3)
            });

            // Мета-блок: Автор/Режиссёр, Жанр и год, Рейтинг
            var metaStack = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };

            if (!string.IsNullOrWhiteSpace(item.Author))
            {
                var role = item.Kind == MediaKind.Movie ? "Режиссёр" : "Автор";
                metaStack.Children.Add(new TextBlock
                {
                    Text = $"{role}: {item.Author}",
                    FontSize = 11.5,
                    Foreground = (Brush)Application.Current.Resources["TextBrush"],
                    FontWeight = FontWeights.Medium,
                    Margin = new Thickness(0, 0, 0, 2)
                });
            }

            var metaText = item.Year > 0 ? $"{item.GenreText}  •  {item.Year} г." : item.GenreText;
            metaStack.Children.Add(new TextBlock
            {
                Text = metaText,
                Style = (Style)Application.Current.Resources["SmallText"],
                FontSize = 11.5,
                Margin = new Thickness(0, 0, 0, 3)
            });

            var ratingRow = new StackPanel { Orientation = Orientation.Horizontal };
            ratingRow.Children.Add(new TextBlock
            {
                Text = "★",
                FontSize = 12,
                Foreground = (Brush)Application.Current.Resources["Accent2Brush"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 3, 0)
            });
            ratingRow.Children.Add(new TextBlock
            {
                Text = $"{item.Rating:F1}",
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI Semibold"),
                Foreground = (Brush)Application.Current.Resources["Accent2Brush"],
                VerticalAlignment = VerticalAlignment.Center
            });
            ratingRow.Children.Add(new TextBlock
            {
                Text = " / 10",
                FontSize = 10.5,
                Foreground = (Brush)Application.Current.Resources["TextFaintBrush"],
                VerticalAlignment = VerticalAlignment.Center
            });
            metaStack.Children.Add(ratingRow);

            ContentPanel.Children.Add(metaStack);

            // Описание (компактное)
            ContentPanel.Children.Add(new TextBlock
            {
                Text = item.Description,
                Style = (Style)Application.Current.Resources["SmallText"],
                FontSize = 11.5,
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 52,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // Кнопка Смотреть / Читать
            var watchButton = new Button
            {
                Style = (Style)Application.Current.Resources["AccentButton"],
                Content = item.Kind == MediaKind.Movie ? "▷  Смотреть" : "▷  Читать",
                Height = 34,
                Padding = new Thickness(14, 0, 14, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 6)
            };
            watchButton.Click += (_, _) => ExternalLinks.Open(item);
            ContentPanel.Children.Add(watchButton);

            // Кнопка Избранного
            var favButton = new Button
            {
                Style = (Style)Application.Current.Resources["GhostButton"],
                Content = FavText(item),
                Height = 34,
                Padding = new Thickness(14, 0, 14, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 12)
            };
            favButton.Click += (_, _) =>
            {
                _data.ToggleFavorite(item.Id);
                var isFav = _data.IsFavorite(item.Id);
                favButton.Content = FavText(item);

                var mainWin = Application.Current.MainWindow as MainWindow;
                if (isFav)
                    mainWin?.ShowToast("Добавлено в избранное", "♥");
                else
                    mainWin?.ShowToast("Удалено из избранного", "♡");

                FavoriteChanged?.Invoke(item);
            };
            ContentPanel.Children.Add(favButton);

            // Раздел "Почему стоит..."
            var reasonsTitle = item.Kind == MediaKind.Movie ? "Почему стоит посмотреть?" : "Почему стоит прочитать?";
            ContentPanel.Children.Add(new TextBlock
            {
                Text = reasonsTitle,
                Style = (Style)Application.Current.Resources["H3"],
                Margin = new Thickness(0, 0, 0, 6)
            });

            var reasons = item.Kind == MediaKind.Movie ? MovieReasons : BookReasons;
            foreach (var reason in reasons)
            {
                var rPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
                var check = new System.Windows.Shapes.Path
                {
                    Data = (Geometry)Application.Current.Resources["IconCheck"],
                    Fill = (Brush)Application.Current.Resources["Accent2Brush"],
                    Width = 11,
                    Height = 11,
                    Stretch = Stretch.Uniform,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                var txt = new TextBlock
                {
                    Text = reason,
                    Style = (Style)Application.Current.Resources["SmallText"],
                    VerticalAlignment = VerticalAlignment.Center
                };
                rPanel.Children.Add(check);
                rPanel.Children.Add(txt);
                ContentPanel.Children.Add(rPanel);
            }

            // Раздел "Похожие"
            ContentPanel.Children.Add(new TextBlock
            {
                Text = "Похожие",
                Style = (Style)Application.Current.Resources["H3"],
                Margin = new Thickness(0, 14, 0, 8)
            });

            var pool = (item.Kind == MediaKind.Movie
                ? _data.Movies.Concat(_data.Books)
                : _data.Books.Concat(_data.Movies)).ToList();

            var similar = RecommendationService.RecommendSimilar(item, pool, limit: 2, favoriteIds: _data.Favorites);
            if (similar.Count == 0)
            {
                ContentPanel.Children.Add(new TextBlock
                {
                    Text = "Нет подходящих похожих произведений",
                    Style = (Style)Application.Current.Resources["SmallText"],
                    Opacity = 0.7
                });
            }
            else
            {
                var similarGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 6) };
                double cardWidth = 104;

                for (int i = 0; i < similar.Count; i++)
                {
                    var sim = similar[i];
                    var card = new CardControl
                    {
                        Width = cardWidth,
                        Margin = (i % 2 == 0) ? new Thickness(0, 0, 8, 0) : new Thickness(0, 0, 0, 0)
                    };
                    card.Initialize(sim, _data);
                    card.OpenRequested += m => OpenRequested?.Invoke(m);
                    card.FavoriteChanged += m => FavoriteChanged?.Invoke(m);
                    similarGrid.Children.Add(card);
                }
                ContentPanel.Children.Add(similarGrid);
            }

            // Плавное появление (Fade-in 180ms)
            var fadeIn = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            ContentPanel.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        private string FavText(MediaItem item) =>
            _data.IsFavorite(item.Id) ? "♥  В избранном" : "♡  Добавить в избранное";

        // ------------------------------------------------------------------
        // Режим «ЕЩЁ ДЛЯ ВАС» (для детальной страницы произведения)
        // ------------------------------------------------------------------
        public void ShowMoreForYou(MediaItem currentItem, bool shuffle = false)
        {
            _isMoreMode = true;
            _currentItemForMore = currentItem;
            if (HeaderTitle != null) HeaderTitle.Text = "ЕЩЁ ДЛЯ ВАС";

            if (shuffle)
                _moreSeedOffset++;
            else
                _moreSeedOffset = 0;

            if (ContentPanel.Children.Count == 0)
            {
                PopulateMoreForYou(currentItem);
                AnimateIn();
                return;
            }

            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(70));
            fadeOut.Completed += (_, _) =>
            {
                PopulateMoreForYou(currentItem);
                AnimateIn();
            };
            ContentPanel.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void PopulateMoreForYou(MediaItem currentItem)
        {
            ContentPanel.Children.Clear();

            var descText = new TextBlock
            {
                Text = "Вам также могут понравиться произведения со схожей атмосферой:",
                Style = (Style)Application.Current.Resources["SmallText"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14),
                Foreground = (Brush)Application.Current.Resources["TextMutedBrush"]
            };
            ContentPanel.Children.Add(descText);

            var pool = _data.AllItems().Where(i => i.Id != currentItem.Id).ToList();
            var similar = RecommendationService.RecommendSimilar(currentItem, pool, limit: 6, favoriteIds: _data.Favorites);
            if (_moreSeedOffset > 0 && similar.Count > 3)
            {
                int shift = _moreSeedOffset % similar.Count;
                similar = similar.Skip(shift).Concat(similar.Take(shift)).ToList();
            }

            var itemsToShow = similar.Take(4).ToList();
            var moreGrid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 4, 0, 10) };
            double cardWidth = 104;

            for (int i = 0; i < itemsToShow.Count; i++)
            {
                var item = itemsToShow[i];
                var card = new CardControl
                {
                    Width = cardWidth,
                    Margin = (i % 2 == 0) ? new Thickness(0, 0, 8, 10) : new Thickness(0, 0, 0, 10)
                };
                card.Initialize(item, _data);
                card.OpenRequested += it => OpenRequested?.Invoke(it);
                card.FavoriteChanged += it => FavoriteChanged?.Invoke(it);
                moreGrid.Children.Add(card);
            }
            ContentPanel.Children.Add(moreGrid);
        }
    }
}

