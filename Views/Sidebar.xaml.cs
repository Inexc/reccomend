using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using System.Windows.Media.Animation;

namespace MovieBookRecommender.Views
{
    public partial class Sidebar : UserControl
    {
        public event Action<string>? Navigate;
        public event Action<string>? ItemReselected;

        private static readonly (string Key, string IconKey, string Label)[] Items =
        {
            ("home", "IconHome", "Главная"),
            ("movies", "IconFilm", "Фильмы"),
            ("books", "IconBook", "Книги"),
            ("favorites", "IconHeart", "Избранное"),
            ("settings", "IconSettings", "Настройки"),
        };

        private static readonly Color MutedColor = Color.FromRgb(0xA6, 0x96, 0x8F);
        private static readonly Color WhiteColor = Color.FromRgb(0xF2, 0xE9, 0xE4);
        private static readonly Color HoverBgColor = Color.FromArgb(0x4D, 0x2A, 0x20, 0x24);

        private class NavRow
        {
            public Border Pill = null!;
            public Border Indicator = null!;
            public System.Windows.Shapes.Path Icon = null!;
            public TextBlock Label = null!;
            public TextBlock? BadgeText;
            public SolidColorBrush BgBrush = null!;
            public SolidColorBrush FgBrush = null!;
            public TranslateTransform Translate = null!;
            public ScaleTransform Scale = null!;
        }

        private readonly Dictionary<string, NavRow> _rows = new();
        private string _active = "home";
        public string ActiveKey => _active;

        public Sidebar()
        {
            InitializeComponent();
            BuildNav();
            SetActive("home");
        }

        private void BrandHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_active == "home")
            {
                ItemReselected?.Invoke("home");
            }
            else
            {
                SetActive("home");
                Navigate?.Invoke("home");
            }
        }

        private void BuildNav()
        {
            NavPanel.Children.Clear();
            _rows.Clear();

            var easeOut = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            var easeIn = new QuadraticEase { EasingMode = EasingMode.EaseIn };

            foreach (var (key, iconKey, label) in Items)
            {
                var bgBrush = new SolidColorBrush(Colors.Transparent);
                var fgBrush = new SolidColorBrush(MutedColor);
                var translate = new TranslateTransform(0, 0);
                var scale = new ScaleTransform(1.0, 1.0);
                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(scale);
                transformGroup.Children.Add(translate);

                var pill = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 2, 0, 2),
                    Padding = new Thickness(8, 8, 10, 8),
                    Cursor = Cursors.Hand,
                    Background = bgBrush,
                    RenderTransformOrigin = new Point(0.5, 0.5),
                    RenderTransform = transformGroup
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Тонкий вертикальный индикатор на левой кромке активного пункта
                var indicator = new Border
                {
                    Width = 3,
                    Height = 14,
                    CornerRadius = new CornerRadius(1.5),
                    Background = Brushes.White,
                    Opacity = 0.0,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };
                Grid.SetColumn(indicator, 0);
                grid.Children.Add(indicator);

                var content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(content, 1);

                var iconPath = new System.Windows.Shapes.Path
                {
                    Data = (Geometry)Application.Current.Resources[iconKey],
                    Fill = fgBrush,
                    Width = 15,
                    Height = 15,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.82
                };
                var labelText = new TextBlock
                {
                    Text = label,
                    FontSize = 12.5,
                    FontFamily = new FontFamily("Segoe UI"),
                    Foreground = fgBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                content.Children.Add(iconPath);
                content.Children.Add(labelText);

                TextBlock? badge = null;
                if (key == "favorites")
                {
                    badge = new TextBlock
                    {
                        FontSize = 11,
                        FontFamily = new FontFamily("Segoe UI Semibold"),
                        Foreground = (Brush)Application.Current.Resources["Accent2Brush"],
                        Margin = new Thickness(6, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Visibility = Visibility.Collapsed
                    };
                    content.Children.Add(badge);
                }

                grid.Children.Add(content);
                pill.Child = grid;

                // Плавные ненавязчивые Hover-эффекты (160-180 ms)
                pill.MouseEnter += (_, _) =>
                {
                    if (_active != key)
                    {
                        bgBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                            new ColorAnimation(HoverBgColor, TimeSpan.FromMilliseconds(160)) { EasingFunction = easeOut });
                        fgBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                            new ColorAnimation(WhiteColor, TimeSpan.FromMilliseconds(160)) { EasingFunction = easeOut });
                        iconPath.BeginAnimation(UIElement.OpacityProperty,
                            new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = easeOut });
                        translate.BeginAnimation(TranslateTransform.XProperty,
                            new DoubleAnimation(2.0, TimeSpan.FromMilliseconds(160)) { EasingFunction = easeOut });
                    }
                };

                pill.MouseLeave += (_, _) =>
                {
                    if (_active != key)
                    {
                        bgBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                            new ColorAnimation(Colors.Transparent, TimeSpan.FromMilliseconds(180)) { EasingFunction = easeIn });
                        fgBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                            new ColorAnimation(MutedColor, TimeSpan.FromMilliseconds(180)) { EasingFunction = easeIn });
                        iconPath.BeginAnimation(UIElement.OpacityProperty,
                            new DoubleAnimation(0.82, TimeSpan.FromMilliseconds(180)) { EasingFunction = easeIn });
                        translate.BeginAnimation(TranslateTransform.XProperty,
                            new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = easeIn });
                    }
                };

                pill.MouseLeftButtonDown += (_, _) =>
                {
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                        new DoubleAnimation(0.97, TimeSpan.FromMilliseconds(70)));
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                        new DoubleAnimation(0.97, TimeSpan.FromMilliseconds(70)));
                };

                pill.MouseLeftButtonUp += (_, _) =>
                {
                    scale.BeginAnimation(ScaleTransform.ScaleXProperty,
                        new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(110)));
                    scale.BeginAnimation(ScaleTransform.ScaleYProperty,
                        new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(110)));

                    if (_active == key)
                    {
                        ItemReselected?.Invoke(key);
                    }
                    else
                    {
                        SetActive(key);
                        Navigate?.Invoke(key);
                    }
                };

                _rows[key] = new NavRow
                {
                    Pill = pill,
                    Indicator = indicator,
                    Icon = iconPath,
                    Label = labelText,
                    BadgeText = badge,
                    BgBrush = bgBrush,
                    FgBrush = fgBrush,
                    Translate = translate,
                    Scale = scale
                };
                NavPanel.Children.Add(pill);
            }
        }

        public void UpdateFavoritesBadge(int count)
        {
            if (_rows.TryGetValue("favorites", out var row) && row.BadgeText != null)
            {
                if (count > 0)
                {
                    row.BadgeText.Text = $"({count})";
                    row.BadgeText.Visibility = Visibility.Visible;
                    row.BadgeText.Foreground = (_active == "favorites")
                        ? Brushes.White
                        : (Brush)Application.Current.Resources["Accent2Brush"];
                }
                else
                {
                    row.BadgeText.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void SetActive(string key)
        {
            _active = key;
            var accentGradient = (Brush)Application.Current.Resources["AccentGradient"];
            var accentGlow = (System.Windows.Media.Effects.Effect)Application.Current.Resources["AccentGlow"];

            foreach (var (k, row) in _rows)
            {
                var isActive = k == key;
                if (isActive)
                {
                    row.Pill.Background = accentGradient;
                    row.Pill.Effect = accentGlow;
                    row.Indicator.Opacity = 1.0;
                    row.FgBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                    row.FgBrush.Color = WhiteColor;
                    row.Label.FontWeight = FontWeights.SemiBold;
                    row.Icon.BeginAnimation(UIElement.OpacityProperty, null);
                    row.Icon.Opacity = 1.0;
                    row.Translate.BeginAnimation(TranslateTransform.XProperty, null);
                    row.Translate.X = 2.0;

                    if (row.BadgeText != null)
                        row.BadgeText.Foreground = Brushes.White;
                }
                else
                {
                    row.Pill.Background = row.BgBrush;
                    row.Pill.Effect = null;
                    row.Indicator.Opacity = 0.0;
                    row.BgBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                    row.BgBrush.Color = Colors.Transparent;
                    row.FgBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                    row.FgBrush.Color = MutedColor;
                    row.Label.FontWeight = FontWeights.Normal;
                    row.Icon.BeginAnimation(UIElement.OpacityProperty, null);
                    row.Icon.Opacity = 0.82;
                    row.Translate.BeginAnimation(TranslateTransform.XProperty, null);
                    row.Translate.X = 0.0;

                    if (row.BadgeText != null)
                        row.BadgeText.Foreground = (Brush)Application.Current.Resources["Accent2Brush"];
                }
            }
        }
    }
}
