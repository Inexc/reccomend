using System.Windows;
using System.Windows.Controls;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    public partial class SettingsPage : UserControl
    {
        private DataManager _data = null!;
        private MainWindow _app = null!;
        private bool _initialized;

        public SettingsPage()
        {
            InitializeComponent();
        }

        public void Initialize(DataManager data, MainWindow app)
        {
            _data = data;
            _app = app;

            ThemeCombo.ItemsSource = new[] { "Тёмная", "Светлая" };
            ThemeCombo.SelectedIndex = 0;

            ScaleCombo.ItemsSource = new[] { "Компактный", "Обычный", "Крупный" };
            ScaleCombo.SelectedIndex = 1;

            UpdateFavoritesCount();

            _initialized = true;
        }

        private void UpdateFavoritesCount()
        {
            var count = _data.GetFavoriteItems().Count;
            FavoritesCountText.Text = count.ToString();
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized) return;
            if (ThemeCombo.SelectedItem as string == "Светлая")
            {
                MessageBox.Show(
                    "В этой версии приложение оптимизировано под тёмную тему.\n" +
                    "Переключение на светлую тему будет добавлено позже.",
                    "Тема", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClearFavorites_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Очистить весь список избранного?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _data.ClearFavorites();
                UpdateFavoritesCount();
                MessageBox.Show("Список избранного очищен.", "Готово",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                _app.RefreshCurrentPage();
            }
        }
    }
}
