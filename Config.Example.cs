namespace MovieBookRecommender
{
    /// <summary>
    /// Пример конфигурации приложения.
    ///
    /// Для загрузки постеров фильмов с TMDb:
    ///   1. Зарегистрируйтесь (бесплатно): https://www.themoviedb.org/signup
    ///   2. Получите API-ключ (v3 auth): https://www.themoviedb.org/settings/api
    ///   3. Либо задайте переменную окружения TMDB_API_KEY,
    ///      либо создайте файл Data/secrets.json:
    ///      {
    ///          "tmdbApiKey": "ВАШ_КЛЮЧ"
    ///      }
    ///
    /// Без ключа фильмы получат аккуратные сгенерированные заглушки.
    /// </summary>
    public static class ConfigExample
    {
        public static readonly string TmdbApiKey = "";
        public static readonly bool UseOnlinePosters = true;
        public static readonly bool Debug = false;
    }
}
