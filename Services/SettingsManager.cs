using System;
using System.IO;
using System.Text.Json;
using MovieBookRecommender.Models;

namespace MovieBookRecommender.Services
{
    public class SettingsManager
    {
        private static readonly string LegacySettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "settings.json");
        public static readonly string UserDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MovieBookRecommender");
        private static readonly string SettingsFile = Path.Combine(UserDataDir, "settings.json");
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public AppSettings Settings { get; private set; } = new();

        public void Load()
        {
            try
            {
                Directory.CreateDirectory(UserDataDir);
                if (!File.Exists(SettingsFile) && File.Exists(LegacySettingsFile))
                {
                    try { File.Copy(LegacySettingsFile, SettingsFile, overwrite: true); } catch { }
                }

                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
                else
                {
                    Settings = new AppSettings();
                }
            }
            catch
            {
                // При ошибке чтения не падаем, используем настройки по умолчанию
                Settings = new AppSettings();
            }
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsFile);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(Settings, JsonOptions);
                File.WriteAllText(SettingsFile, json);
            }
            catch
            {
                // Игнорируем ошибки записи, приложение не должно падать
            }
        }
    }
}

