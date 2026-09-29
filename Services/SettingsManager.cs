using System;
using System.IO;
using System.Text.Json;
using MovieBookRecommender.Models;

namespace MovieBookRecommender.Services
{
    public class SettingsManager
    {
        private static readonly string SettingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "settings.json");
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public AppSettings Settings { get; private set; } = new();

        public void Load()
        {
            try
            {
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
