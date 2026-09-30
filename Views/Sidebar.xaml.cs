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

        private static readonly (string Key, string IconKey, string Label)[] Items =
        {
            ("home", "IconHome", "Главная"),
            ("movies", "IconFilm", "Фильмы"),
            ("books", "IconBook", "Книги"),
            ("favorites", "IconHeart", "Избранное"),
            ("settings", "IconSettings", "Настройки"),
        };

        private static readonly Color MutedColor = Color.FromRgb(0xA3, 0x9A, 0x95);
        private static readonly Color WhiteColor = Colors.White;
        private static readonly Color HoverBgColor = Color.FromArgb(0x80, 0x22, 0x1A, 0x1E);

        private class NavRow
        {
            public Border Pill = null!;
            public System.Windows.Shapes.Path Icon = null!;
            public TextBlock Label = null!;
            public TextBlock? BadgeText;
            public SolidColorBrush BgBrush = null!;
            public SolidColorBrush FgBrush = null!;
        }

        private readonly Dictionary<string, NavRow> _rows = new();
        private string _active = "home";

        public Sidebar()
        {
            InitializeComponent();
            BuildNav();
            SetActive("home");
        }

        private void BuildNav()
        {
            foreach (var (key, iconKey, label) in Items)
            {
                var bgBrush = new SolidColorBrush(Colors.Transparent);
                var fgBrush = new SolidColorBrush(MutedColor);

                var pill = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 3, 0, 3),
                    Padding = new Thickness(10, 8, 10, 8),
                    Cursor = Cursors.Hand,
                    Background = bgBrush
                };

                var content = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                var iconPath = new System.Windows.Shapes.Path
                {
                    Data = (Geometry)Application.Current.Resources[iconKey],
                    Fill = fgBrush,
                    Width = 15,
                    Height = 15,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(2, 0, 10, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.82
                };
                var labelText = new TextBlock
                {
                    Text = label, FontSize = 12.5,
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

                pill.Child = content;

                pill.MouseEnter += (_, _) =>
                {
                    if (_active != key)
                    {
                        bgBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                            new ColorAnimation(HoverBgColor, TimeSpan.FromMilliseconds(160)));
                        fgBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                            new ColorAnimation(WhiteColor, TimeSpan.FromMilliseconds(160)));
                        iconPath.BeginAnimation(UIElement.OpacityProperty,
                            new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(160)));
                    }
                };
                pill.MouseLeave += (_, _) =>
                {
                    if (_active != key)
                    {
                        bgBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                            new ColorAnimation(Colors.Transparent, TimeSpan.FromMilliseconds(180)));
                        fgBrush.BeginAnimation(SolidColorBrush.ColorProperty,
                            new ColorAnimation(MutedColor, TimeSpan.FromMilliseconds(180)));
                        iconPath.BeginAnimation(UIElement.OpacityProperty,
                            new DoubleAnimation(0.82, TimeSpan.FromMilliseconds(180)));
                    }
                };
                pill.MouseLeftButtonUp += (_, _) =>
                {
                    SetActive(key);
                    Navigate?.Invoke(key);
                };

                _rows[key] = new NavRow
                {
                    Pill = pill,
                    Icon = iconPath,
                    Label = labelText,
                    BadgeText = badge,
                    BgBrush = bgBrush,
                    FgBrush = fgBrush
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
            foreach (var (k, row) in _rows)
            {
                var isActive = k == key;
                if (isActive)
                {
                    row.Pill.Background = (Brush)Application.Current.Resources["AccentGradient"];
                    row.Pill.Effect = (System.Windows.Media.Effects.Effect)Application.Current.Resources["AccentGlow"];
                    row.FgBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                    row.FgBrush.Color = WhiteColor;
                    row.Icon.BeginAnimation(UIElement.OpacityProperty, null);
                    row.Icon.Opacity = 1.0;
                    if (row.BadgeText != null)
                        row.BadgeText.Foreground = Brushes.White;
                }
                else
                {
                    row.Pill.Background = row.BgBrush;
                    row.Pill.Effect = null;
                    row.BgBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                    row.BgBrush.Color = Colors.Transparent;
                    row.FgBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
                    row.FgBrush.Color = MutedColor;
                    row.Icon.BeginAnimation(UIElement.OpacityProperty, null);
                    row.Icon.Opacity = 0.82;
                    if (row.BadgeText != null)
                        row.BadgeText.Foreground = (Brush)Application.Current.Resources["Accent2Brush"];
                }
            }
        }
    }
}
