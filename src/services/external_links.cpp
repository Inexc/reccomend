#include "external_links.h"
#include <QUrl>
#include <QDesktopServices>

QString ExternalLinks::buildUrl(const MediaItem& item) {
    if (item.kind == MediaKind::Movie) {
        return QString("https://www.kinopoisk.ru/index.php?kp_query=%1")
            .arg(QString(QUrl::toPercentEncoding(item.title)));
    } else {
        QString query = item.title;
        if (!item.author.isEmpty()) {
            query += " " + item.author;
        }
        return QString("https://www.litres.ru/pages/biblio_search/?q=%1")
            .arg(QString(QUrl::toPercentEncoding(query)));
    }
}

void ExternalLinks::open(const MediaItem& item) {
    QString urlStr = buildUrl(item);
    QDesktopServices::openUrl(QUrl(urlStr));
}

