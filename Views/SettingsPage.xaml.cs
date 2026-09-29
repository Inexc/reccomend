using System;
using System.Windows;
using System.Windows.Controls;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    public partial class SettingsPage : UserControl
    {
        private DataManager _data = null!;
        private MainWindow _app = null!;
        private SettingsManager _settingsManager = null!;
        private bool _initialized;

        public SettingsPage()
        {
            InitializeComponent();
        }

        public void Initialize(DataManager data, MainWindow app, SettingsManager settingsManager)
        {
            _data = data;
            _app = app;
            _settingsManager = settingsManager;

            var themes = new[] { "Тёмная (Тёплый нуар)", "Тёмная (Индиго)", "Тёмная (Изумруд)" };
            ThemeCombo.ItemsSource = themes;
            var currentTheme = _settingsManager.Settings.Theme;
            var themeIdx = Array.IndexOf(themes, currentTheme);
            ThemeCombo.SelectedIndex = themeIdx >= 0 ? themeIdx : 0;

            var scales = new[] { "Компактный (90%)", "Обычный (100%)", "Крупный (110%)" };
            ScaleCombo.ItemsSource = scales;
            var currentScale = _settingsManager.Settings.Scale;
            var scaleIdx = Array.IndexOf(scales, currentScale);
            ScaleCombo.SelectedIndex = scaleIdx >= 0 ? scaleIdx : 1;

            UpdateFavoritesCount();

            _initialized = true;
        }

        public void UpdateFavoritesCount()
        {
            var count = _data.GetFavoriteItems().Count;
            FavoritesCountText.Text = count.ToString();
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized || ThemeCombo.SelectedItem == null) return;
            var theme = ThemeCombo.SelectedItem.ToString()!;
            _app.ApplyTheme(theme);
            _app.ShowToast("Настройки сохранены", "⚙");
        }

        private void ScaleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized || ScaleCombo.SelectedItem == null) return;
            var scale = ScaleCombo.SelectedItem.ToString()!;
            _app.ApplyScale(scale);
            _app.ShowToast("Настройки сохранены", "⚙");
        }

        private void ClearFavorites_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы действительно хотите очистить весь список избранного?",
                "Подтверждение",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _data.ClearFavorites();
                UpdateFavoritesCount();
                _app.OnFavoriteChanged(null);
                _app.ShowToast("Список избранного очищен", "✓");
            }
        }
    }
}
