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

        public static List<MediaItem> RecommendSimilar(MediaItem seed, IEnumerable<MediaItem> pool, int limit = 3)
        {
            return pool
                .Where(item => item.Id != seed.Id)
                .Select(item => (Item: item, Score: Score(seed, item)))
                .Where(pair => pair.Score > 0)
                .OrderByDescending(pair => pair.Score)
                .ThenByDescending(pair => pair.Item.Rating)
                .Take(limit)
                .Select(pair => pair.Item)
                .ToList();
        }

        /// <summary>
        /// Формирует персонализированный список «Рекомендуем для вас».
        /// Если у пользователя есть избранное — взвешивает жанры избранного и предлагает подходящее.
        /// </summary>
        public static List<MediaItem> BuildPersonalizedFeed(IEnumerable<MediaItem> pool, HashSet<string> favoriteIds, int limit = 12)
        {
            var poolList = pool.ToList();
            if (poolList.Count == 0)
                return new List<MediaItem>();

            var favItems = poolList.Where(i => favoriteIds.Contains(i.Id)).ToList();
            if (favItems.Count == 0)
            {
                // Если избранного пока нет — выдаем самые высокорейтинговые произведения
                return poolList.OrderByDescending(i => i.Rating).Take(limit).ToList();
            }

            // Частотный словарь жанров из избранного
            var genreWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in favItems.SelectMany(i => i.Genre))
            {
                genreWeights[g] = genreWeights.GetValueOrDefault(g, 0) + 1;
            }

            var candidates = poolList.Where(i => !favoriteIds.Contains(i.Id)).ToList();
            if (candidates.Count == 0)
                candidates = poolList;

            return candidates
                .Select(item =>
                {
                    var genreScore = item.Genre.Sum(g => genreWeights.GetValueOrDefault(g, 0) * 3.5);
                    var ratingScore = item.Rating * 1.5;
                    var totalScore = genreScore + ratingScore;
                    return (Item: item, Score: totalScore);
                })
                .OrderByDescending(pair => pair.Score)
                .ThenByDescending(pair => pair.Item.Rating)
                .Take(limit)
                .Select(pair => pair.Item)
                .ToList();
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

