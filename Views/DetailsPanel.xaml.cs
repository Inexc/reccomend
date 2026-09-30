using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
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
            if (_isMoreMode && _currentItemForMore != null)
            {
                ShowMoreForYou(_currentItemForMore, shuffle: true);
                return;
            }
            RefreshRequested?.Invoke();
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

            // Постер
            var posterBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                ClipToBounds = true,
                Height = 230,
                Margin = new Thickness(0, 0, 0, 12),
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
                FontSize = 17,
                FontFamily = new FontFamily("Segoe UI Semibold"),
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 4)
            });

            // Мета-блок: Автор/Режиссёр, Жанр и год, Рейтинг
            var metaStack = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };

            if (!string.IsNullOrWhiteSpace(item.Author))
            {
                var role = item.Kind == MediaKind.Movie ? "Режиссёр" : "Автор";
                metaStack.Children.Add(new TextBlock
                {
                    Text = $"{role}: {item.Author}",
                    FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["TextBrush"],
                    FontWeight = FontWeights.Medium,
                    Margin = new Thickness(0, 0, 0, 3)
                });
            }

            var metaText = item.Year > 0 ? $"{item.GenreText}  •  {item.Year} г." : item.GenreText;
            metaStack.Children.Add(new TextBlock
            {
                Text = metaText,
                Style = (Style)Application.Current.Resources["SmallText"],
                Margin = new Thickness(0, 0, 0, 4)
            });

            var ratingRow = new StackPanel { Orientation = Orientation.Horizontal };
            ratingRow.Children.Add(new TextBlock
            {
                Text = "★",
                FontSize = 13,
                Foreground = (Brush)Application.Current.Resources["Accent2Brush"],
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0)
            });
            ratingRow.Children.Add(new TextBlock
            {
                Text = $"{item.Rating:F1}",
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI Semibold"),
                Foreground = (Brush)Application.Current.Resources["Accent2Brush"],
                VerticalAlignment = VerticalAlignment.Center
            });
            ratingRow.Children.Add(new TextBlock
            {
                Text = " / 10",
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextFaintBrush"],
                VerticalAlignment = VerticalAlignment.Center
            });
            metaStack.Children.Add(ratingRow);

            ContentPanel.Children.Add(metaStack);

            // Описание
            ContentPanel.Children.Add(new TextBlock
            {
                Text = item.Description,
                Style = (Style)Application.Current.Resources["SmallText"],
                TextWrapping = TextWrapping.Wrap,
                MaxHeight = 70,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 0, 0, 14)
            });

            // Кнопка Смотреть / Читать
            var watchButton = new Button
            {
                Style = (Style)Application.Current.Resources["AccentButton"],
                Content = item.Kind == MediaKind.Movie ? "▷  Смотреть" : "▷  Читать",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 8)
            };
            watchButton.Click += (_, _) => ExternalLinks.Open(item);
            ContentPanel.Children.Add(watchButton);

            // Кнопка Избранного
            var favButton = new Button
            {
                Style = (Style)Application.Current.Resources["GhostButton"],
                Content = FavText(item),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Margin = new Thickness(0, 0, 0, 16)
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

            var similarRow = new StackPanel { Orientation = Orientation.Horizontal };
            var pool = (item.Kind == MediaKind.Movie
                ? _data.Movies.Concat(_data.Books)
                : _data.Books.Concat(_data.Movies)).ToList();

            var similar = RecommendationService.RecommendSimilar(item, pool, 3);
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
                foreach (var sim in similar)
                {
                    var mini = new MiniCardControl { Margin = new Thickness(0, 0, 10, 0) };
                    mini.Initialize(sim);
                    mini.OpenRequested += m => OpenRequested?.Invoke(m);
                    similarRow.Children.Add(mini);
                }
                ContentPanel.Children.Add(similarRow);
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
            var similar = RecommendationService.RecommendSimilar(currentItem, pool, 6);
            if (_moreSeedOffset > 0 && similar.Count > 3)
            {
                int shift = _moreSeedOffset % similar.Count;
                similar = similar.Skip(shift).Concat(similar.Take(shift)).ToList();
            }

            var itemsToShow = similar.Take(4).ToList();

            foreach (var item in itemsToShow)
            {
                var card = CreateMoreItemCard(item);
                ContentPanel.Children.Add(card);
            }
        }

        private Border CreateMoreItemCard(MediaItem item)
        {
            var border = new Border
            {
                Background = (Brush)Application.Current.Resources["CardBrush"],
                BorderBrush = (Brush)Application.Current.Resources["BorderSubtleBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Постер мини
            var posterBorder = new Border
            {
                Width = 44,
                Height = 62,
                CornerRadius = new CornerRadius(6),
                ClipToBounds = true,
                Margin = new Thickness(0, 0, 10, 0),
                Background = (Brush)Application.Current.Resources["BgBrush"]
            };
            var img = new Image
            {
                Stretch = Stretch.UniformToFill,
                Source = PosterService.GetImage(item)
            };
            posterBorder.Child = img;
            Grid.SetColumn(posterBorder, 0);
            grid.Children.Add(posterBorder);

            // Текстовый стек
            var textStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var kindTag = item.Kind == MediaKind.Movie ? "Фильм" : "Книга";
            var tagText = new TextBlock
            {
                Text = $"{kindTag}  •  ★ {item.Rating:F1}",
                FontSize = 10.5,
                FontFamily = new FontFamily("Segoe UI Semibold"),
                Foreground = (Brush)Application.Current.Resources["Accent2Brush"],
                Margin = new Thickness(0, 0, 0, 2)
            };
            textStack.Children.Add(tagText);

            var title = new TextBlock
            {
                Text = item.Title,
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI Semibold"),
                Foreground = (Brush)Application.Current.Resources["TextBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxHeight = 36,
                TextWrapping = TextWrapping.Wrap
            };
            textStack.Children.Add(title);

            var sub = new TextBlock
            {
                Text = !string.IsNullOrWhiteSpace(item.Author) ? item.Author : item.GenreText,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextMutedBrush"],
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 2, 0, 0)
            };
            textStack.Children.Add(sub);

            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            border.Child = grid;

            // Hover effects
            border.MouseEnter += (_, _) =>
            {
                border.Background = (Brush)Application.Current.Resources["CardHoverBrush"];
                border.BorderBrush = (Brush)Application.Current.Resources["AccentBrush"];
            };
            border.MouseLeave += (_, _) =>
            {
                border.Background = (Brush)Application.Current.Resources["CardBrush"];
                border.BorderBrush = (Brush)Application.Current.Resources["BorderSubtleBrush"];
            };

            border.MouseLeftButtonUp += (_, _) =>
            {
                OpenRequested?.Invoke(item);
            };

            return border;
        }
    }
}

