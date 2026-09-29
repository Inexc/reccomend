#pragma once
#include <QString>

namespace Config {
    // Вставьте ключ API TMDb (v3 auth) при необходимости:
    // https://www.themoviedb.org/settings/api
    inline const QString TmdbApiKey = "";

    // Если false — оба онлайн-источника отключены,
    // всегда используются сгенерированные стильные заглушки.
    inline const bool UseOnlinePosters = true;
}

