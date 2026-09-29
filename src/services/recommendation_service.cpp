#include "recommendation_service.h"
#include <cmath>
#include <algorithm>

double RecommendationService::score(const MediaItem& seed, const MediaItem& candidate) {
    if (seed.id == candidate.id) {
        return -1.0;
    }

    QSet<QString> seedGenres;
    for (const auto& g : seed.genre) {
        seedGenres.insert(g);
    }

    int overlap = 0;
    for (const auto& g : candidate.genre) {
        if (seedGenres.contains(g)) {
            overlap++;
        }
    }

    if (overlap == 0) {
        return 0.0;
    }

    double ratingDiff = std::abs(seed.rating - candidate.rating);
    double ratingScore = std::max(0.0, 1.0 - ratingDiff / 10.0);

    return overlap * 10.0 + ratingScore * 2.0 + candidate.rating * 0.1;
}

QList<MediaItem> RecommendationService::recommendSimilar(const MediaItem& seed, const QList<MediaItem>& pool, int limit) {
    struct ScoredItem {
        MediaItem item;
        double score;
    };

    QList<ScoredItem> scored;
    for (const auto& item : pool) {
        double s = score(seed, item);
        if (s > 0) {
            scored.append({ item, s });
        }
    }

    std::sort(scored.begin(), scored.end(), [](const ScoredItem& a, const ScoredItem& b) {
        return a.score > b.score;
    });

    QList<MediaItem> result;
    int count = std::min(limit, static_cast<int>(scored.size()));
    for (int i = 0; i < count; ++i) {
        result.append(scored[i].item);
    }
    return result;
}

const MediaItem* RecommendationService::pickFeatured(const QList<MediaItem>& pool, const QSet<QString>& favoriteIds) {
    if (pool.isEmpty()) {
        return nullptr;
    }

    // Check if there are favorite items in pool
    QList<MediaItem> favoriteItems;
    for (const auto& item : pool) {
        if (favoriteIds.contains(item.id)) {
            favoriteItems.append(item);
        }
    }

    if (!favoriteItems.isEmpty()) {
        QSet<QString> likedGenres;
        for (const auto& item : favoriteItems) {
            for (const auto& g : item.genre) {
                likedGenres.insert(g);
            }
        }

        const MediaItem* best = nullptr;
        double bestScore = -1.0;

        for (const auto& item : pool) {
            if (favoriteIds.contains(item.id)) continue;

            int overlap = 0;
            for (const auto& g : item.genre) {
                if (likedGenres.contains(g)) overlap++;
            }

            double score = overlap * 10.0 + item.rating;
            if (score > bestScore) {
                bestScore = score;
                best = &item;
            }
        }

        if (best != nullptr) {
            return best;
        }
    }

    // Fallback: pick highest rating
    const MediaItem* highest = &pool[0];
    for (int i = 1; i < pool.size(); ++i) {
        if (pool[i].rating > highest->rating) {
            highest = &pool[i];
        }
    }
    return highest;
}

