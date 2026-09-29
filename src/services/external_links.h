#pragma once

#include <QString>
#include "../models/media_item.h"

class ExternalLinks {
public:
    static QString buildUrl(const MediaItem& item);
    static void open(const MediaItem& item);
};

