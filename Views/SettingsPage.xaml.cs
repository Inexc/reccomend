using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using MovieBookRecommender.Services;

namespace MovieBookRecommender.Views
{
    /// <summary>
    /// Страница настроек приложения:
    /// 1. Внешний вид (Тема оформления и Масштаб интерфейса).
    /// 2. Приложение (Автосохранение и Анимации с ToggleSwitch).
    /// 3. Данные (Динамический счётчик избранного и изолированная опасная зона с подтверждением).
    /// 4. О приложении (версия 2.0, архитектура, JSON).
    /// </summary>
    public partial class SettingsPage : UserControl
    {
        private DataManager _data = null!;
        private MainWindow _app = null!;
        private SettingsManager _settingsManager = null!;
        private bool _initialized;
        private bool _isTwoColumnLayout = true;

        public SettingsPage()
        {
            InitializeComponent();
        }

        public void Initialize(DataManager data, MainWindow app, SettingsManager settingsManager)
        {
            _data = data;
            _app = app;
            _settingsManager = settingsManager;

            // 1. Темы оформления
            var themes = new[] { "Тёмная (Тёплый нуар)", "Тёмная (Индиго)", "Тёмная (Изумруд)" };
            ThemeCombo.ItemsSource = themes;
            var currentTheme = _settingsManager.Settings.Theme;
            var themeIdx = Array.IndexOf(themes, currentTheme);
            ThemeCombo.SelectedIndex = themeIdx >= 0 ? themeIdx : 0;

            // 2. Размер интерфейса
            var scales = new[] { "Компактный (90%)", "Обычный (100%)", "Крупный (110%)" };
            ScaleCombo.ItemsSource = scales;
            var currentScale = _settingsManager.Settings.Scale;
            var scaleIdx = Array.IndexOf(scales, currentScale);
            ScaleCombo.SelectedIndex = scaleIdx >= 0 ? scaleIdx : 1;

            // 3. Параметры приложения (ToggleSwitch)
            AutoSaveToggle.IsChecked = _settingsManager.Settings.AutoSave;
            AnimationsToggle.IsChecked = _settingsManager.Settings.EnableAnimations;

            // 4. Счётчик избранного
            UpdateFavoritesCount();

            _initialized = true;
        }

        public void UpdateFavoritesCount()
        {
            if (_data == null || FavoritesCountText == null) return;
            var count = _data.Favorites.Count;
            FavoritesCountText.Text = $"Сохранено элементов: {count}";
        }

        private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized || ThemeCombo.SelectedItem == null) return;
            var theme = ThemeCombo.SelectedItem.ToString()!;
            try
            {
                _app.ApplyTheme(theme);
                _app.ShowToast("Настройки сохранены", "⚙");
            }
            catch
            {
                _app.ShowToast("Не удалось применить настройку", "!");
            }
        }

        private void ScaleCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized || ScaleCombo.SelectedItem == null) return;
            var scale = ScaleCombo.SelectedItem.ToString()!;
            try
            {
                _app.ApplyScale(scale);
                _app.ShowToast("Настройки сохранены", "⚙");
            }
            catch
            {
                _app.ShowToast("Не удалось применить настройку", "!");
            }
        }

        private void AutoSaveToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            var isAutoSave = AutoSaveToggle.IsChecked ?? true;
            _settingsManager.Settings.AutoSave = isAutoSave;
            _settingsManager.Save();
            _app.ShowToast("Настройки сохранены", "⚙");
        }

        private void AnimationsToggle_Click(object sender, RoutedEventArgs e)
        {
            if (!_initialized) return;
            var isAnimations = AnimationsToggle.IsChecked ?? true;
            _settingsManager.Settings.EnableAnimations = isAnimations;
            _settingsManager.Save();
            _app.ShowToast("Настройки сохранены", "⚙");
        }

        // ------------------------------------------------------------------
        // Очистка избранного с подтверждением (Опасная зона)
        // ------------------------------------------------------------------
        private void OpenClearDialog_Click(object sender, RoutedEventArgs e)
        {
            if (ConfirmDialog.Visibility == Visibility.Visible)
            {
                CancelClear_Click(sender, e);
                return;
            }

            ConfirmDialog.Visibility = Visibility.Visible;
            ConfirmTranslate.Y = -6;

            var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180));
            var slide = new DoubleAnimation(-6.0, 0.0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            ConfirmDialog.BeginAnimation(OpacityProperty, fadeIn);
            ConfirmTranslate.BeginAnimation(System.Windows.Media.TranslateTransform.YProperty, slide);
        }

        private void CancelClear_Click(object sender, RoutedEventArgs e)
        {
            var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(140));
            fadeOut.Completed += (_, _) => ConfirmDialog.Visibility = Visibility.Collapsed;
            ConfirmDialog.BeginAnimation(OpacityProperty, fadeOut);
        }

        private void ConfirmClear_Click(object sender, RoutedEventArgs e)
        {
            ConfirmDialog.Visibility = Visibility.Collapsed;

            _data.ClearFavorites();
            UpdateFavoritesCount();

            // Синхронизация всех открытых представлений (Главная, Избранное, Сайдбар)
            _app.OnFavoriteChanged(null);
            _app.ShowToast("Избранное очищено", "✓");
        }

        // ------------------------------------------------------------------
        // Адаптивная вёрстка (две колонки на десктопе, одна на узких экранах)
        // ------------------------------------------------------------------
        private void PageScroll_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateResponsiveLayout(e.NewSize.Width);
        }

        private void UpdateResponsiveLayout(double availableWidth)
        {
            if (SettingsGrid == null || LeftCol == null || RightCol == null || GapCol == null || RightPanel == null) return;

            bool shouldBeTwoColumn = availableWidth >= 860;
            if (shouldBeTwoColumn == _isTwoColumnLayout) return;
            _isTwoColumnLayout = shouldBeTwoColumn;

            if (shouldBeTwoColumn)
            {
                GapCol.Width = new GridLength(18);
                RightCol.Width = new GridLength(1, GridUnitType.Star);
                Grid.SetColumn(RightPanel, 2);
                Grid.SetRow(RightPanel, 0);
                RightPanel.Margin = new Thickness(0);
            }
            else
            {
                GapCol.Width = new GridLength(0);
                RightCol.Width = new GridLength(0);
                Grid.SetColumn(RightPanel, 0);
                Grid.SetRow(RightPanel, 1);
                RightPanel.Margin = new Thickness(0, 16, 0, 0);
            }
        }
    }
}

