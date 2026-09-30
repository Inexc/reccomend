namespace MovieBookRecommender
{
    /// <summary>
    /// Настройки приложения.
    ///
    /// Чтобы подтягивались НАСТОЯЩИЕ постеры фильмов из TMDb:
    ///   1. Зарегистрируйтесь (бесплатно): https://www.themoviedb.org/signup
    ///   2. Получите API-ключ (v3 auth): https://www.themoviedb.org/settings/api
    ///   3. Вставьте его в TmdbApiKey ниже.
    ///
    /// Без ключа фильмы получат сгенерированную цветную заглушку —
    /// приложение не падает и работает полностью.
    ///
    /// Обложки книг подтягиваются через Open Library Covers API —
    /// это бесплатный публичный сервис, ключ не нужен.
    /// </summary>
    public static class Config
    {
        public static readonly string TmdbApiKey = "6a4f66adec55b3608261b4e118fab95f";

        /// <summary>Если false — оба онлайн-источника отключены,
        /// всегда используются сгенерированные заглушки.</summary>
        public static readonly bool UseOnlinePosters = true;

        /// <summary>Включает подробное логирование в poster_errors.log.</summary>
        public static readonly bool Debug = false;
    }
}
