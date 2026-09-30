using System;
using System.IO;
using System.Text.Json;

namespace MovieBookRecommender
{
    /// <summary>
    /// Настройки приложения.
    ///
    /// Чтобы подтягивались НАСТОЯЩИЕ постеры фильмов из TMDb:
    ///   1. Зарегистрируйтесь (бесплатно): https://www.themoviedb.org/signup
    ///   2. Получите API-ключ (v3 auth): https://www.themoviedb.org/settings/api
    ///   3. Задайте переменную окружения TMDB_API_KEY
    ///      либо создайте файл Data/secrets.json с содержимым:
    ///      { "tmdbApiKey": "ВАШ_КЛЮЧ" }
    ///
    /// Без ключа фильмы получат сгенерированную цветную заглушку —
    /// приложение не падает и работает полностью.
    ///
    /// Обложки книг подтягиваются через Open Library Covers API —
    /// это бесплатный публичный сервис, ключ не нужен.
    /// </summary>
    public static class Config
    {
        public static readonly string TmdbApiKey = LoadTmdbApiKey();

        /// <summary>Если false — оба онлайн-источника отключены,
        /// всегда используются сгенерированные заглушки.</summary>
        public static readonly bool UseOnlinePosters = true;

        /// <summary>Включает подробное логирование в poster_errors.log.</summary>
        public static readonly bool Debug = false;

        private static string LoadTmdbApiKey()
        {
            // 1. Проверяем переменную окружения
            var envKey = Environment.GetEnvironmentVariable("TMDB_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey))
                return envKey.Trim();

            // 2. Проверяем файл Data/secrets.json
            var pathsToTry = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "secrets.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "Data", "secrets.json"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MovieBookRecommender", "secrets.json")
            };

            foreach (var path in pathsToTry)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        using var doc = JsonDocument.Parse(json);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("tmdbApiKey", out var prop) ||
                            root.TryGetProperty("TmdbApiKey", out prop) ||
                            root.TryGetProperty("apiKey", out prop) ||
                            root.TryGetProperty("ApiKey", out prop))
                        {
                            var key = prop.GetString();
                            if (!string.IsNullOrWhiteSpace(key))
                                return key.Trim();
                        }
                    }
                }
                catch
                {
                    // Игнорируем ошибки чтения файла секретов
                }
            }

            return "";
        }
    }
}
