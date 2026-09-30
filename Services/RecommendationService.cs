using System;
using System.Collections.Generic;
using System.Linq;
using MovieBookRecommender.Models;

namespace MovieBookRecommender.Services
{
    /// <summary>
    /// Система рекомендаций:
    /// 1. Подбор похожих для конкретного произведения (seed).
    /// 2. Персонализированный фид «Рекомендуем для вас» на основе избранного.
    /// 3. Выбор featured-произведения для Hero-баннера.
    /// </summary>
    public static class RecommendationService
    {
        private static double Score(MediaItem seed, MediaItem candidate)
        {
            if (seed.Id == candidate.Id)
                return -1;

            var seedGenres = new HashSet<string>(seed.Genre, StringComparer.OrdinalIgnoreCase);
            var overlap = candidate.Genre.Count(g => seedGenres.Contains(g));
            
            // Если жанры не пересеклись напрямую, проверяем того же автора/режиссёра
            if (overlap == 0)
            {
                if (!string.IsNullOrWhiteSpace(seed.Author) &&
                    string.Equals(seed.Author.Trim(), candidate.Author.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    overlap = 1;
                }
                else
                {
                    return 0;
                }
            }

            var ratingDiff = Math.Abs(seed.Rating - candidate.Rating);
            var ratingScore = Math.Max(0.0, 1.0 - ratingDiff / 10.0);

            // Бонус за соответствие типа произведения (фильм к фильму, книга к книге),
            // но кросс-рекомендации также возможны при высоком совпадении жанров
            var kindBonus = (seed.Kind == candidate.Kind) ? 4.0 : 0.0;

            return overlap * 10.0 + ratingScore * 2.0 + candidate.Rating * 0.1 + kindBonus;
        }

        public static List<MediaItem> RecommendSimilar(MediaItem seed, IEnumerable<MediaItem> pool, int limit = 4)
        {
            var poolList = pool.Where(item => item.Id != seed.Id).ToList();
            var ranked = poolList
                .Select(item => (Item: item, Score: Score(seed, item)))
                .Where(pair => pair.Score > 0)
                .OrderByDescending(pair => pair.Score)
                .ThenByDescending(pair => pair.Item.Rating)
                .Take(limit)
                .Select(pair => pair.Item)
                .ToList();

            if (ranked.Count < limit && poolList.Count > ranked.Count)
            {
                var added = new HashSet<string>(ranked.Select(r => r.Id));
                // Сначала того же типа по рейтингу
                foreach (var item in poolList.Where(i => i.Kind == seed.Kind).OrderByDescending(i => i.Rating))
                {
                    if (ranked.Count >= limit) break;
                    if (added.Add(item.Id))
                        ranked.Add(item);
                }
                // Затем любые оставшиеся
                foreach (var item in poolList.OrderByDescending(i => i.Rating))
                {
                    if (ranked.Count >= limit) break;
                    if (added.Add(item.Id))
                        ranked.Add(item);
                }
            }

            return ranked;
        }

        /// <summary>
        /// Чередует фильмы и книги, чтобы избежать однообразия («Фильм, Фильм, Фильм, Фильм»).
        /// </summary>
        public static List<MediaItem> InterleaveKinds(IEnumerable<MediaItem> items, int count)
        {
            var list = items.ToList();
            var movies = list.Where(i => i.Kind == MediaKind.Movie).ToList();
            var books = list.Where(i => i.Kind == MediaKind.Book).ToList();

            var result = new List<MediaItem>();
            int m = 0, b = 0;

            // Начинаем с того типа, у которого выше рейтинг первого элемента
            bool takeMovie = movies.Count > 0 && (books.Count == 0 || movies[0].Rating >= books[0].Rating);

            while (result.Count < count && (m < movies.Count || b < books.Count))
            {
                if (takeMovie && m < movies.Count)
                {
                    result.Add(movies[m++]);
                    if (b < books.Count) takeMovie = false;
                }
                else if (!takeMovie && b < books.Count)
                {
                    result.Add(books[b++]);
                    if (m < movies.Count) takeMovie = true;
                }
                else if (m < movies.Count)
                {
                    result.Add(movies[m++]);
                }
                else if (b < books.Count)
                {
                    result.Add(books[b++]);
                }
                else
                {
                    break;
                }
            }

            return result;
        }

        /// <summary>
        /// Формирует персонализированный список «Рекомендуем для вас».
        /// Если у пользователя есть избранное — взвешивает жанры избранного и предлагает подходящее.
        /// Чередует фильмы и книги для разнообразия.
        /// </summary>
        public static List<MediaItem> BuildPersonalizedFeed(IEnumerable<MediaItem> pool, HashSet<string> favoriteIds, int limit = 4, IEnumerable<string>? excludeIds = null)
        {
            var poolList = pool.ToList();
            if (poolList.Count == 0)
                return new List<MediaItem>();

            var excludeSet = excludeIds != null ? new HashSet<string>(excludeIds) : new HashSet<string>();

            var favItems = poolList.Where(i => favoriteIds.Contains(i.Id)).ToList();
            if (favItems.Count == 0)
            {
                // Если избранного пока нет — выдаем самые высокорейтинговые произведения, чередуя типы
                var nonExcluded = poolList.Where(i => !excludeSet.Contains(i.Id)).OrderByDescending(i => i.Rating).ToList();
                return InterleaveKinds(nonExcluded, limit);
            }

            // Частотный словарь жанров из избранного
            var genreWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in favItems.SelectMany(i => i.Genre))
            {
                genreWeights[g] = genreWeights.GetValueOrDefault(g, 0) + 1;
            }

            var candidates = poolList.Where(i => !favoriteIds.Contains(i.Id) && !excludeSet.Contains(i.Id)).ToList();
            if (candidates.Count == 0)
                candidates = poolList.Where(i => !excludeSet.Contains(i.Id)).ToList();

            var ranked = candidates
                .Select(item =>
                {
                    var genreScore = item.Genre.Sum(g => genreWeights.GetValueOrDefault(g, 0) * 3.5);
                    var ratingScore = item.Rating * 1.5;
                    var totalScore = genreScore + ratingScore;
                    return (Item: item, Score: totalScore);
                })
                .OrderByDescending(pair => pair.Score)
                .ThenByDescending(pair => pair.Item.Rating)
                .Select(pair => pair.Item)
                .ToList();

            return InterleaveKinds(ranked, limit);
        }

        /// <summary>
        /// Секция «Популярное»: элементы с наивысшим рейтингом, без дублирования уже показанных.
        /// </summary>
        public static List<MediaItem> BuildPopularFeed(IEnumerable<MediaItem> pool, IEnumerable<string>? excludeIds = null, int limit = 4)
        {
            var excludeSet = excludeIds != null ? new HashSet<string>(excludeIds) : new HashSet<string>();
            var candidates = pool.Where(i => !excludeSet.Contains(i.Id)).OrderByDescending(i => i.Rating).ToList();
            return InterleaveKinds(candidates, limit);
        }

        /// <summary>
        /// Секция «Новинки»: элементы с самыми свежими годами выпуска, без дублирования уже показанных.
        /// </summary>
        public static List<MediaItem> BuildNewReleasesFeed(IEnumerable<MediaItem> pool, IEnumerable<string>? excludeIds = null, int limit = 4)
        {
            var excludeSet = excludeIds != null ? new HashSet<string>(excludeIds) : new HashSet<string>();
            var candidates = pool.Where(i => !excludeSet.Contains(i.Id))
                                 .OrderByDescending(i => i.Year)
                                 .ThenByDescending(i => i.Rating)
                                 .ToList();
            return InterleaveKinds(candidates, limit);
        }

        /// <summary>
        /// Возвращает список кандидатов для Hero-блока (для возможности переключения рекомендаций).
        /// </summary>
        public static List<MediaItem> PickFeaturedCandidates(IEnumerable<MediaItem> pool, HashSet<string> favoriteIds, int count = 5)
        {
            var poolList = pool.ToList();
            if (poolList.Count == 0) return new List<MediaItem>();

            var result = new List<MediaItem>();
            var addedIds = new HashSet<string>();

            // 1. Главная персонализированная рекомендация
            var topPick = PickFeatured(poolList, favoriteIds);
            if (topPick != null)
            {
                result.Add(topPick);
                addedIds.Add(topPick.Id);
            }

            // 2. Лучший фильм
            var bestMovie = poolList.Where(i => i.Kind == MediaKind.Movie && !addedIds.Contains(i.Id))
                                    .OrderByDescending(i => i.Rating)
                                    .FirstOrDefault();
            if (bestMovie != null)
            {
                result.Add(bestMovie);
                addedIds.Add(bestMovie.Id);
            }

            // 3. Лучшая книга
            var bestBook = poolList.Where(i => i.Kind == MediaKind.Book && !addedIds.Contains(i.Id))
                                   .OrderByDescending(i => i.Rating)
                                   .FirstOrDefault();
            if (bestBook != null)
            {
                result.Add(bestBook);
                addedIds.Add(bestBook.Id);
            }

            // 4. Самая свежая новинка с высоким рейтингом
            var newest = poolList.Where(i => !addedIds.Contains(i.Id))
                                 .OrderByDescending(i => i.Year)
                                 .ThenByDescending(i => i.Rating)
                                 .FirstOrDefault();
            if (newest != null)
            {
                result.Add(newest);
                addedIds.Add(newest.Id);
            }

            // 5. Оставшийся топ по рейтингу
            foreach (var item in poolList.OrderByDescending(i => i.Rating))
            {
                if (result.Count >= count) break;
                if (!addedIds.Contains(item.Id))
                {
                    result.Add(item);
                    addedIds.Add(item.Id);
                }
            }

            return result;
        }

        /// <summary>Выбирает объект для блока Hero / «Подбор для вас».</summary>
        public static MediaItem? PickFeatured(IEnumerable<MediaItem> pool, HashSet<string> favoriteIds)
        {
            var poolList = pool.ToList();
            if (poolList.Count == 0)
                return null;

            var favoriteItems = poolList.Where(i => favoriteIds.Contains(i.Id)).ToList();
            if (favoriteItems.Count > 0)
            {
                var likedGenres = new HashSet<string>(favoriteItems.SelectMany(i => i.Genre), StringComparer.OrdinalIgnoreCase);
                var candidates = poolList.Where(i => !favoriteIds.Contains(i.Id)).ToList();

                MediaItem? best = null;
                double bestScore = -1;
                foreach (var item in candidates)
                {
                    var overlap = item.Genre.Count(g => likedGenres.Contains(g));
                    var score = overlap * 10 + item.Rating;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = item;
                    }
                }
                if (best != null)
                    return best;
            }

            return poolList.OrderByDescending(i => i.Rating).First();
        }
    }
}

