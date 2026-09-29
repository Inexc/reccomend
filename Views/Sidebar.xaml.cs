using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MovieBookRecommender.Views
{
    public partial class Sidebar : UserControl
    {
        public event Action<string>? Navigate;

        private static readonly (string Key, string Icon, string Label)[] Items =
        {
            ("home", "⌂", "Главная"),
            ("movies", "▷", "Фильмы"),
            ("books", "▤", "Книги"),
            ("favorites", "♡", "Избранное"),
            ("settings", "⚙", "Настройки"),
        };

        private class NavRow
        {
            public Border Pill = null!;
            public TextBlock Icon = null!;
            public TextBlock Label = null!;
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
            foreach (var (key, icon, label) in Items)
            {
                var pill = new Border
                {
                    CornerRadius = new CornerRadius(8),
                    Margin = new Thickness(0, 4, 0, 4),
                    Padding = new Thickness(10, 8, 10, 8),
                    Cursor = Cursors.Hand,
                    Background = Brushes.Transparent
                };

                var content = new StackPanel { Orientation = Orientation.Horizontal };
                var iconText = new TextBlock
                {
                    Text = icon, FontSize = 13, Width = 20, Margin = new Thickness(0, 0, 8, 0),
                    Foreground = (Brush)Application.Current.Resources["TextMutedBrush"]
                };
                var labelText = new TextBlock
                {
                    Text = label, FontSize = 12,
                    Foreground = (Brush)Application.Current.Resources["TextMutedBrush"],
                    VerticalAlignment = VerticalAlignment.Center
                };
                content.Children.Add(iconText);
                content.Children.Add(labelText);
                pill.Child = content;

                pill.MouseEnter += (_, _) =>
                {
                    if (_active != key) pill.Background = (Brush)Application.Current.Resources["CardBrush"];
                };
                pill.MouseLeave += (_, _) =>
                {
                    if (_active != key) pill.Background = Brushes.Transparent;
                };
                pill.MouseLeftButtonUp += (_, _) =>
                {
                    SetActive(key);
                    Navigate?.Invoke(key);
                };

                _rows[key] = new NavRow { Pill = pill, Icon = iconText, Label = labelText };
                NavPanel.Children.Add(pill);
            }
        }

        public void SetActive(string key)
        {
            _active = key;
            foreach (var (k, row) in _rows)
            {
                var isActive = k == key;
                row.Pill.Background = isActive
                    ? (Brush)Application.Current.Resources["AccentGradient"]
                    : Brushes.Transparent;
                row.Pill.Effect = isActive
                    ? (System.Windows.Media.Effects.Effect)Application.Current.Resources["AccentGlow"]
                    : null;
                row.Icon.Foreground = isActive ? Brushes.White : (Brush)Application.Current.Resources["TextMutedBrush"];
                row.Label.Foreground = isActive ? Brushes.White : (Brush)Application.Current.Resources["TextMutedBrush"];
            }
        }
    }
}
