using System;
using System.Collections.Generic;
using System.Linq;
using MovieBookRecommender.Models;

namespace MovieBookRecommender.Services
{
    /// <summary>
    /// Параметры запроса рекомендаций для единого движка.
    /// </summary>
    public class RecommendationOptions
    {
        /// <summary>Текущее произведение (для блока «Похожие произведения»).</summary>
        public MediaItem? SeedItem { get; set; }

        /// <summary>Список идентификаторов избранного пользователя.</summary>
        public HashSet<string>? FavoriteIds { get; set; }

        /// <summary>Элементы, которые строго не должны присутствовать в выдаче.</summary>
        public IEnumerable<string>? ExcludeIds { get; set; }

        /// <summary>Опциональный фильтр по типу произведения.</summary>
        public MediaKind? KindFilter { get; set; }

        /// <summary>Смещение для небольшого разнообразия кандидатов с близкими оценками.</summary>
        public int VarietyOffset { get; set; } = 0;

        /// <summary>Чередовать фильмы и книги для сбалансированной выдачи.</summary>
        public bool InterleaveMediaKinds { get; set; } = true;

        /// <summary>Исключать элементы из избранного из выдачи рекомендаций.</summary>
        public bool ExcludeFavorites { get; set; } = true;
    }

    /// <summary>
    /// Единая система персональных рекомендаций:
    /// 1. «Рекомендуем для вас» на Главной (на основе избранного или общего топа).
    /// 2. «Похожие произведения» в Деталях (на основе seed-произведения + вкусов пользователя).
    /// 3. «На основе вашего избранного» в Избранном (персонализированный фид исключая сохранённое).
    /// 4. Подбор кандидатов для Hero и сопутствующих подборок.
    /// </summary>
    public static class RecommendationService
    {
        /// <summary>
        /// Центральный метод системы рекомендаций. Все блоки приложения обращаются к нему.
        /// </summary>
        public static List<MediaItem> GetRecommendations(
            IEnumerable<MediaItem> pool,
            RecommendationOptions options,
            int limit = 4)
        {
            var poolList = pool.Where(i => i != null).ToList();
            if (poolList.Count == 0 || limit <= 0)
                return new List<MediaItem>();

            // 1. Формируем множество исключений
            var hardExclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (options.ExcludeIds != null)
            {
                foreach (var id in options.ExcludeIds)
                {
                    if (!string.IsNullOrWhiteSpace(id))
                        hardExclude.Add(id);
                }
            }
            if (options.SeedItem != null)
                hardExclude.Add(options.SeedItem.Id);

            var excludeSet = new HashSet<string>(hardExclude, StringComparer.OrdinalIgnoreCase);
            if (options.ExcludeFavorites && options.FavoriteIds != null)
            {
                foreach (var favId in options.FavoriteIds)
                    excludeSet.Add(favId);
            }

            // Кандидаты, удовлетворяющие фильтру типа и исключениям
            var candidates = poolList.Where(i => !excludeSet.Contains(i.Id));
            if (options.KindFilter.HasValue)
                candidates = candidates.Where(i => i.Kind == options.KindFilter.Value);
            var candidateList = candidates.ToList();

            bool favoritesRelaxed = false;

            // Порядок ослабления ограничений при отсутствии кандидатов:
            // 1) Сначала снимаем только «избранное», сохраняя hardExclude (SeedItem и ExcludeIds)
            if (candidateList.Count == 0 && options.ExcludeFavorites && options.FavoriteIds != null && options.FavoriteIds.Count > 0)
            {
                favoritesRelaxed = true;
                var relaxedQuery = poolList.Where(i => !hardExclude.Contains(i.Id));
                if (options.KindFilter.HasValue)
                    relaxedQuery = relaxedQuery.Where(i => i.Kind == options.KindFilter.Value);
                candidateList = relaxedQuery.ToList();
            }

            // 2) При необходимости снимаем ExcludeIds. SeedItem не возвращать никогда.
            if (candidateList.Count == 0 && options.ExcludeIds != null)
            {
                var seedOnlyQuery = poolList.Where(i => options.SeedItem == null || !string.Equals(i.Id, options.SeedItem.Id, StringComparison.OrdinalIgnoreCase));
                if (options.KindFilter.HasValue)
                    seedOnlyQuery = seedOnlyQuery.Where(i => i.Kind == options.KindFilter.Value);
                candidateList = seedOnlyQuery.ToList();
            }

            if (candidateList.Count == 0)
                return new List<MediaItem>();

            // 2. Строим профиль вкусов пользователя на основе избранного
            var favItems = poolList
                .Where(i => options.FavoriteIds != null && options.FavoriteIds.Contains(i.Id))
                .ToList();

            var userGenreWeights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var userAuthors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int movieFavs = 0;
            int bookFavs = 0;

            if (favItems.Count > 0)
            {
                foreach (var fav in favItems)
                {
                    // Оценка сохранённого произведения влияет на вес жанра (шедевры 9.0 дают больший вес, чем 7.0)
                    double favWeight = Math.Max(1.0, fav.Rating - 5.0);

                    foreach (var genre in fav.Genre)
                    {
                        userGenreWeights[genre] = userGenreWeights.GetValueOrDefault(genre, 0.0) + favWeight;
                    }

                    if (!string.IsNullOrWhiteSpace(fav.Author))
                        userAuthors.Add(fav.Author.Trim());

                    if (fav.Kind == MediaKind.Movie) movieFavs++;
                    else if (fav.Kind == MediaKind.Book) bookFavs++;
                }

                // Нормировка весов жанров на максимальный вес, чтобы значения лежали в диапазоне [0..1]
                if (userGenreWeights.Count > 0)
                {
                    double maxWeight = userGenreWeights.Values.Max();
                    if (maxWeight > 0.0)
                    {
                        foreach (var key in userGenreWeights.Keys.ToList())
                        {
                            userGenreWeights[key] /= maxWeight;
                        }
                    }
                }
            }

            double movieAffinity = favItems.Count > 0 ? (double)movieFavs / favItems.Count : 0.5;
            double bookAffinity = favItems.Count > 0 ? (double)bookFavs / favItems.Count : 0.5;

            // 3. Вычисляем релевантность каждого кандидата
            var ranked = candidateList
                .Select(candidate => (
                    Item: candidate,
                    Score: ScoreCandidate(candidate, options, userGenreWeights, userAuthors, movieAffinity, bookAffinity, favItems.Count)
                ))
                .OrderByDescending(p => p.Score)
                .ThenByDescending(p => p.Item.Rating)
                .Select(p => p.Item)
                .ToList();

            // 4. Разнообразие авторов и чередование типов контента
            List<MediaItem> result;
            if (options.InterleaveMediaKinds && options.KindFilter == null)
            {
                result = InterleaveKindsWithDiversity(ranked, limit);
            }
            else
            {
                result = PickWithAuthorDiversity(ranked, limit);
            }

            // 5. Защитное дополнение:
            // Если подходящих рекомендаций недостаточно, аккуратно дополняем высокорейтинговыми элементами
            if (result.Count < limit && poolList.Count > result.Count)
            {
                var addedIds = new HashSet<string>(result.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);

                // Базовые исключения для дополнения: уже добавленные + hardExclude (SeedItem, ExcludeIds).
                // Если избранное не снималось, то и FavoriteIds.
                var fallbackExclude = new HashSet<string>(addedIds, StringComparer.OrdinalIgnoreCase);
                foreach (var id in hardExclude)
                    fallbackExclude.Add(id);

                if (!favoritesRelaxed && options.ExcludeFavorites && options.FavoriteIds != null)
                {
                    foreach (var fid in options.FavoriteIds)
                        fallbackExclude.Add(fid);
                }

                var fallbackQuery = poolList.Where(i => !fallbackExclude.Contains(i.Id));
                if (options.KindFilter.HasValue)
                {
                    fallbackQuery = fallbackQuery.Where(i => i.Kind == options.KindFilter.Value);
                }

                var fallbackPool = fallbackQuery
                    .OrderByDescending(i => i.Rating)
                    .ToList();

                foreach (var item in fallbackPool)
                {
                    if (result.Count >= limit) break;
                    if (addedIds.Add(item.Id))
                        result.Add(item);
                }
            }

            return result;
        }

        /// <summary>
        /// Комплексная оценка кандидата.
        /// </summary>
        private static double ScoreCandidate(
            MediaItem c,
            RecommendationOptions options,
            Dictionary<string, double> userGenreWeights,
            HashSet<string> userAuthors,
            double movieAffinity,
            double bookAffinity,
            int totalFavCount)
        {
            double score = 0.0;

            // 1. Базовый рейтинг кандидата (1.8 * Rating)
            score += c.Rating * 1.8;

            // 2. Сходство с SeedItem (для «Похожие произведения» / DetailPage)
            if (options.SeedItem != null)
            {
                var seed = options.SeedItem;
                var seedGenres = new HashSet<string>(seed.Genre, StringComparer.OrdinalIgnoreCase);
                int overlap = c.Genre.Count(g => seedGenres.Contains(g));

                // Совпадение по жанрам с текущим произведением
                score += overlap * 14.0;

                // Тот же автор или режиссёр
                if (!string.IsNullOrWhiteSpace(seed.Author) && !string.IsNullOrWhiteSpace(c.Author) &&
                    string.Equals(seed.Author.Trim(), c.Author.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    score += 18.0;
                }

                // Тот же тип произведения (фильм к фильму, книга к книге)
                if (c.Kind == seed.Kind)
                {
                    score += 5.0;
                }

                // Близость рейтингов (шедевры к шедеврам)
                double diff = Math.Abs(seed.Rating - c.Rating);
                score += Math.Max(0.0, 2.0 - diff * 0.5);
            }

            // 3. Вкус пользователя на основе Избранного
            if (totalFavCount > 0)
            {
                // Вкус пользователя по жанрам на основе профиля избранного:
                // userGenreWeights предварительно нормирован в диапазон [0..1] делением на максимальный вес.
                // Суммируются веса совпадающих жанров с множителем 7.0 и ограничением 14.0 баллов,
                // чтобы вклад вкуса (макс. 14) был сопоставим с вкладом рейтинга (Rating * 1.8 ≈ 14–17)
                // и не подавлял общую выдачу как при 1, так и при 8+ произведениях в избранном.
                double userGenreScore = 0.0;
                foreach (var g in c.Genre)
                {
                    if (userGenreWeights.TryGetValue(g, out var gw))
                    {
                        userGenreScore += gw * 7.0;
                    }
                }
                userGenreScore = Math.Min(14.0, userGenreScore);

                // Если задан SeedItem, вкусы пользователя служат вспомогательным бустером (35%),
                // если SeedItem нет (Главная или Избранное) — вкусы пользователя определяют основной приоритет (100%)
                score += options.SeedItem != null ? userGenreScore * 0.35 : userGenreScore;

                // Тот же автор/режиссёр, что и в избранном
                if (!string.IsNullOrWhiteSpace(c.Author) && userAuthors.Contains(c.Author.Trim()))
                {
                    score += 7.0;
                }

                // Предпочтение по типу контента (только если нет конкретного seed)
                if (options.SeedItem == null)
                {
                    double kindBonus = (c.Kind == MediaKind.Movie ? movieAffinity : bookAffinity) * 3.5;
                    score += kindBonus;
                }
            }

            // 4. Мягкий фактор года выпуска (лёгкое предпочтение признанной классики и свежих хитов)
            if (c.Year > 1990)
            {
                score += Math.Min(1.5, (c.Year - 1990) * 0.04);
            }

            // 5. Небольшое разнообразие (Requirement 5)
            // Детерминированный псевдо-джиттер от 0.0 до 1.5, зависящий от ID и VarietyOffset,
            // чтобы среди кандидатов с одинаково высокими оценками не выпадал один и тот же порядок
            uint jitterHash = (ComputeFnv1a(c.Id) ^ ((uint)options.VarietyOffset * 16777619u)) % 100u;
            score += (jitterHash / 100.0) * 1.5;

            return score;
        }

        private static uint ComputeFnv1a(string text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }
            return hash;
        }

        /// <summary>
        /// Выбирает элементы с ограничением дублирования одного автора (не более 2 на подборку).
        /// </summary>
        private static List<MediaItem> PickWithAuthorDiversity(IEnumerable<MediaItem> candidates, int count)
        {
            var result = new List<MediaItem>();
            var authorCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var deferred = new List<MediaItem>();

            foreach (var item in candidates)
            {
                if (result.Count >= count) break;

                var author = item.Author?.Trim() ?? "";
                if (!string.IsNullOrEmpty(author))
                {
                    int current = authorCounts.GetValueOrDefault(author, 0);
                    if (current >= 2)
                    {
                        deferred.Add(item);
                        continue;
                    }
                    authorCounts[author] = current + 1;
                }

                result.Add(item);
            }

            // Если не набрали count из-за ограничения автора, добираем отложенные
            foreach (var item in deferred)
            {
                if (result.Count >= count) break;
                result.Add(item);
            }

            return result;
        }

        /// <summary>
        /// Чередует фильмы и книги для поддержания мультимедийного баланса коллекции.
        /// </summary>
        private static List<MediaItem> InterleaveKindsWithDiversity(IEnumerable<MediaItem> items, int count)
        {
            var list = items.ToList();
            var movies = PickWithAuthorDiversity(list.Where(i => i.Kind == MediaKind.Movie), int.MaxValue);
            var books = PickWithAuthorDiversity(list.Where(i => i.Kind == MediaKind.Book), int.MaxValue);

            var result = new List<MediaItem>();
            var authorCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var deferred = new List<MediaItem>();

            int m = 0, b = 0;

            // Начинаем с того типа, у которого выше рейтинг первого элемента
            bool takeMovie = movies.Count > 0 && (books.Count == 0 || movies[0].Rating >= books[0].Rating);

            while (result.Count < count && (m < movies.Count || b < books.Count))
            {
                MediaItem? candidate = null;

                if (takeMovie && m < movies.Count)
                {
                    candidate = movies[m++];
                    if (b < books.Count) takeMovie = false;
                }
                else if (!takeMovie && b < books.Count)
                {
                    candidate = books[b++];
                    if (m < movies.Count) takeMovie = true;
                }
                else if (m < movies.Count)
                {
                    candidate = movies[m++];
                }
                else if (b < books.Count)
                {
                    candidate = books[b++];
                }
                else
                {
                    break;
                }

                if (candidate != null)
                {
                    var author = candidate.Author?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(author))
                    {
                        int current = authorCounts.GetValueOrDefault(author, 0);
                        if (current >= 2)
                        {
                            deferred.Add(candidate);
                            continue;
                        }
                        authorCounts[author] = current + 1;
                    }
                    result.Add(candidate);
                }
            }

            // Если не набрали count из-за ограничения автора, добираем отложенные
            foreach (var item in deferred)
            {
                if (result.Count >= count) break;
                result.Add(item);
            }

            return result;
        }

        // ==================================================================
        // ПУБЛИЧНЫЕ МЕТОДЫ (Обратная совместимость со всеми страницами)
        // ==================================================================

        /// <summary>
        /// Формирует персональный список «Рекомендуем для вас» (Главная)
        /// и «На основе вашего избранного» (Избранное).
        /// </summary>
        public static List<MediaItem> BuildPersonalizedFeed(
            IEnumerable<MediaItem> pool,
            HashSet<string> favoriteIds,
            int limit = 4,
            IEnumerable<string>? excludeIds = null,
            int varietyOffset = 0)
        {
            return GetRecommendations(pool, new RecommendationOptions
            {
                FavoriteIds = favoriteIds,
                ExcludeIds = excludeIds,
                VarietyOffset = varietyOffset,
                InterleaveMediaKinds = true
            }, limit);
        }

        /// <summary>
        /// Подбор «Похожие произведения» (Детальный экран и боковая панель).
        /// Учитывает совпадение с выбранным произведением, вкусы пользователя и исключает дубли.
        /// </summary>
        public static List<MediaItem> RecommendSimilar(
            MediaItem seed,
            IEnumerable<MediaItem> pool,
            int limit = 4,
            HashSet<string>? favoriteIds = null,
            IEnumerable<string>? excludeIds = null,
            int varietyOffset = 0)
        {
            return GetRecommendations(pool, new RecommendationOptions
            {
                SeedItem = seed,
                FavoriteIds = favoriteIds,
                ExcludeIds = excludeIds,
                VarietyOffset = varietyOffset,
                InterleaveMediaKinds = false
            }, limit);
        }

        /// <summary>
        /// Секция «Популярное»: общепризнанные произведения с наивысшими оценками.
        /// </summary>
        public static List<MediaItem> BuildPopularFeed(
            IEnumerable<MediaItem> pool,
            IEnumerable<string>? excludeIds = null,
            int limit = 4,
            int varietyOffset = 0)
        {
            return GetRecommendations(pool, new RecommendationOptions
            {
                ExcludeIds = excludeIds,
                VarietyOffset = varietyOffset,
                InterleaveMediaKinds = true
            }, limit);
        }

        /// <summary>
        /// Секция «Новинки»: свежие произведения из коллекции с высокими оценками.
        /// </summary>
        public static List<MediaItem> BuildNewReleasesFeed(
            IEnumerable<MediaItem> pool,
            IEnumerable<string>? excludeIds = null,
            int limit = 4,
            int varietyOffset = 0)
        {
            var poolList = pool.ToList();
            var excludeSet = excludeIds != null ? new HashSet<string>(excludeIds) : new HashSet<string>();

            // Для новинок фильтруем пул по свежести года выпуска
            var candidates = poolList
                .Where(i => !excludeSet.Contains(i.Id))
                .OrderByDescending(i => i.Year)
                .ThenByDescending(i => i.Rating)
                .Take(limit * 3)
                .ToList();

            return GetRecommendations(candidates, new RecommendationOptions
            {
                ExcludeIds = excludeIds,
                VarietyOffset = varietyOffset,
                InterleaveMediaKinds = true
            }, limit);
        }

        /// <summary>
        /// Выбирает главный объект для блока Hero / «Подбор для вас».
        /// </summary>
        public static MediaItem? PickFeatured(
            IEnumerable<MediaItem> pool,
            HashSet<string> favoriteIds,
            int varietyOffset = 0)
        {
            var picks = GetRecommendations(pool, new RecommendationOptions
            {
                FavoriteIds = favoriteIds,
                VarietyOffset = varietyOffset,
                InterleaveMediaKinds = true
            }, limit: 1);

            return picks.FirstOrDefault();
        }

        /// <summary>
        /// Возвращает список разнообразных кандидатов для переключения в Hero-блоке (кнопка «Сменить»).
        /// </summary>
        public static List<MediaItem> PickFeaturedCandidates(
            IEnumerable<MediaItem> pool,
            HashSet<string> favoriteIds,
            int count = 6,
            int varietyOffset = 0)
        {
            var poolList = pool.ToList();
            if (poolList.Count == 0) return new List<MediaItem>();

            var result = new List<MediaItem>();
            var addedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. Главная персонализированная рекомендация на основе профиля
            var topPicks = GetRecommendations(poolList, new RecommendationOptions
            {
                FavoriteIds = favoriteIds,
                VarietyOffset = varietyOffset,
                InterleaveMediaKinds = true
            }, limit: 2);

            foreach (var pick in topPicks)
            {
                if (addedIds.Add(pick.Id))
                    result.Add(pick);
            }

            // 2. Лучший фильм
            var bestMovie = poolList
                .Where(i => i.Kind == MediaKind.Movie && !addedIds.Contains(i.Id) && !favoriteIds.Contains(i.Id))
                .OrderByDescending(i => i.Rating)
                .FirstOrDefault();
            if (bestMovie != null && addedIds.Add(bestMovie.Id))
                result.Add(bestMovie);

            // 3. Лучшая книга
            var bestBook = poolList
                .Where(i => i.Kind == MediaKind.Book && !addedIds.Contains(i.Id) && !favoriteIds.Contains(i.Id))
                .OrderByDescending(i => i.Rating)
                .FirstOrDefault();
            if (bestBook != null && addedIds.Add(bestBook.Id))
                result.Add(bestBook);

            // 4. Свежий хит
            var newest = poolList
                .Where(i => !addedIds.Contains(i.Id) && !favoriteIds.Contains(i.Id))
                .OrderByDescending(i => i.Year)
                .ThenByDescending(i => i.Rating)
                .FirstOrDefault();
            if (newest != null && addedIds.Add(newest.Id))
                result.Add(newest);

            // 5. Дополняем до count из оставшихся
            foreach (var item in poolList.OrderByDescending(i => i.Rating))
            {
                if (result.Count >= count) break;
                if (!favoriteIds.Contains(item.Id) && addedIds.Add(item.Id))
                    result.Add(item);
            }

            return result;
        }

        /// <summary>
        /// Вспомогательный метод для чередования типов произведений.
        /// </summary>
        public static List<MediaItem> InterleaveKinds(IEnumerable<MediaItem> items, int count)
        {
            return InterleaveKindsWithDiversity(items, count);
        }
    }
}

