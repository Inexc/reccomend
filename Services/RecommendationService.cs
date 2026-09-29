using System.Collections.Generic;
using System.Linq;
using MovieBookRecommender.Models;

namespace MovieBookRecommender.Services
{
    /// <summary>
    /// Простая система рекомендаций без использования искусственного интеллекта.
    /// Похожесть объектов считается по количеству совпадающих жанров
    /// (основной вес) и близости рейтинга (второстепенный вес).
    /// </summary>
    public static class RecommendationService
    {
        private static double Score(MediaItem seed, MediaItem candidate)
        {
            if (seed.Id == candidate.Id)
                return -1;

            var seedGenres = new HashSet<string>(seed.Genre);
            var overlap = candidate.Genre.Count(g => seedGenres.Contains(g));
            if (overlap == 0)
                return 0;

            var ratingDiff = System.Math.Abs(seed.Rating - candidate.Rating);
            var ratingScore = System.Math.Max(0.0, 1.0 - ratingDiff / 10.0);

            return overlap * 10 + ratingScore * 2 + candidate.Rating * 0.1;
        }

        public static List<MediaItem> RecommendSimilar(MediaItem seed, IEnumerable<MediaItem> pool, int limit = 3)
        {
            return pool
                .Select(item => (Item: item, Score: Score(seed, item)))
                .Where(pair => pair.Score > 0)
                .OrderByDescending(pair => pair.Score)
                .Take(limit)
                .Select(pair => pair.Item)
                .ToList();
        }

        /// <summary>Выбирает объект для блока «Подбор для вас»: если есть
        /// избранное — что-то из его жанров, иначе — самый высокий рейтинг.</summary>
        public static MediaItem? PickFeatured(IEnumerable<MediaItem> pool, HashSet<string> favoriteIds)
        {
            var poolList = pool.ToList();
            if (poolList.Count == 0)
                return null;

            var favoriteItems = poolList.Where(i => favoriteIds.Contains(i.Id)).ToList();
            if (favoriteItems.Count > 0)
            {
                var likedGenres = new HashSet<string>(favoriteItems.SelectMany(i => i.Genre));
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
