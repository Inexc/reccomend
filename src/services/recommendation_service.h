#pragma once

#include <QList>
#include <QSet>
#include "../models/media_item.h"

class RecommendationService {
public:
    static double score(const MediaItem& seed, const MediaItem& candidate);
    static QList<MediaItem> recommendSimilar(const MediaItem& seed, const QList<MediaItem>& pool, int limit = 3);
    static MediaItem pickFeatured(const QList<MediaItem>& pool, const QSet<QString>& favoriteIds);
};

