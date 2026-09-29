#include "poster_service.h"
#include "../config.h"
#include "data_manager.h"
#include <QPainter>
#include <QLinearGradient>
#include <QFont>
#include <QFileInfo>
#include <QDir>
#include <QUrl>
#include <QUrlQuery>
#include <QJsonDocument>
#include <QJsonObject>
#include <QJsonArray>

PosterService::PosterService(QObject* parent)
    : QObject(parent), m_nam(new QNetworkAccessManager(this)) {
}

PosterService& PosterService::instance() {
    static PosterService s_instance;
    return s_instance;
}

QString PosterService::getPosterPath(const MediaItem& item) {
    return DataManager::resolvePath(item.poster);
}

void PosterService::ensurePoster(const MediaItem& item) {
    QString fullPath = getPosterPath(item);
    if (QFile::exists(fullPath)) {
        return;
    }

    QFileInfo fi(fullPath);
    QDir().mkpath(fi.dir().absolutePath());

    // Generate placeholder first so UI displays immediately
    generatePlaceholder(item, fullPath);

    // If online posters enabled, try fetching real poster
    if (Config::UseOnlinePosters) {
        if (item.kind == MediaKind::Movie) {
            fetchMoviePoster(item, fullPath);
        } else {
            fetchBookCover(item, fullPath);
        }
    }
}

void PosterService::generatePlaceholder(const MediaItem& item, const QString& destPath) {
    const int w = 300;
    const int h = 440;
    QImage image(w, h, QImage::Format_ARGB32_Premultiplied);

    unsigned int hash = qHash(item.title);
    int seed = hash % 3;

    QColor topColor;
    QColor bottomColor(0x22, 0x1A, 0x1E);

    if (seed == 0) {
        topColor = QColor(0x2E, 0x23, 0x27);
    } else if (seed == 1) {
        topColor = QColor(0x2B, 0x21, 0x29);
    } else {
        topColor = QColor(0x29, 0x21, 0x24);
    }

    QPainter painter(&image);
    painter.setRenderHint(QPainter::Antialiasing);
    painter.setRenderHint(QPainter::TextAntialiasing);

    QLinearGradient grad(0, 0, 0, h);
    grad.setColorAt(0.0, topColor);
    grad.setColorAt(1.0, bottomColor);

    painter.fillRect(0, 0, w, h, grad);

    QString letter = "?";
    QString trimmed = item.title.trimmed();
    if (!trimmed.isEmpty()) {
        letter = QString(trimmed[0]).toUpper();
    }

    QFont font("Segoe UI", 68, QFont::DemiBold);
    painter.setFont(font);
    painter.setPen(QColor(242, 233, 228, 70));
    painter.drawText(QRect(0, 0, w, h), Qt::AlignCenter, letter);
    painter.end();

    QFileInfo fi(destPath);
    QDir().mkpath(fi.dir().absolutePath());
    image.save(destPath, "PNG");
}

void PosterService::fetchMoviePoster(const MediaItem& item, const QString& destPath) {
    if (Config::TmdbApiKey.trimmed().isEmpty()) {
        return;
    }

    QUrl url("https://api.themoviedb.org/3/search/movie");
    QUrlQuery query;
    query.addQueryItem("api_key", Config::TmdbApiKey);
    query.addQueryItem("query", item.title);
    query.addQueryItem("language", "ru-RU");
    if (item.year > 0) {
        query.addQueryItem("year", QString::number(item.year));
    }
    url.setQuery(query);

    QNetworkRequest request(url);
    request.setHeader(QNetworkRequest::UserAgentHeader, "movie-book-recommender-qt/1.0");

    QNetworkReply* reply = m_nam->get(request);
    connect(reply, &QNetworkReply::finished, this, [this, reply, destPath, itemId = item.id]() {
        reply->deleteLater();
        if (reply->error() != QNetworkReply::NoError) {
            return;
        }

        QByteArray data = reply->readAll();
        QJsonDocument doc = QJsonDocument::fromJson(data);
        if (!doc.isObject()) return;

        QJsonArray results = doc.object().value("results").toArray();
        if (results.isEmpty()) return;

        QJsonObject first = results[0].toObject();
        QString posterPath = first.value("poster_path").toString();
        if (posterPath.isEmpty()) return;

        QString imgUrl = "https://image.tmdb.org/t/p/w500" + posterPath;
        downloadImage(imgUrl, destPath, itemId);
    });
}

void PosterService::fetchBookCover(const MediaItem& item, const QString& destPath) {
    QUrl url("https://openlibrary.org/search.json");
    QUrlQuery query;
    query.addQueryItem("title", item.title);
    query.addQueryItem("limit", "5");
    url.setQuery(query);

    QNetworkRequest request(url);
    request.setHeader(QNetworkRequest::UserAgentHeader, "movie-book-recommender-qt/1.0");

    QNetworkReply* reply = m_nam->get(request);
    connect(reply, &QNetworkReply::finished, this, [this, reply, destPath, itemId = item.id]() {
        reply->deleteLater();
        if (reply->error() != QNetworkReply::NoError) {
            return;
        }

        QByteArray data = reply->readAll();
        QJsonDocument doc = QJsonDocument::fromJson(data);
        if (!doc.isObject()) return;

        QJsonArray docs = doc.object().value("docs").toArray();
        qint64 coverId = -1;
        for (const auto& d : docs) {
            QJsonObject obj = d.toObject();
            if (obj.contains("cover_i") && obj.value("cover_i").isDouble()) {
                coverId = static_cast<qint64>(obj.value("cover_i").toDouble());
                break;
            }
        }

        if (coverId <= 0) return;

        QString imgUrl = QString("https://covers.openlibrary.org/b/id/%1-L.jpg").arg(coverId);
        downloadImage(imgUrl, destPath, itemId);
    });
}

void PosterService::downloadImage(const QString& url, const QString& destPath, const QString& itemId) {
    QNetworkRequest request(url);
    request.setHeader(QNetworkRequest::UserAgentHeader, "movie-book-recommender-qt/1.0");

    QNetworkReply* reply = m_nam->get(request);
    connect(reply, &QNetworkReply::finished, this, [this, reply, destPath, itemId]() {
        reply->deleteLater();
        if (reply->error() != QNetworkReply::NoError) {
            return;
        }

        QByteArray imgData = reply->readAll();
        QImage image;
        if (image.loadFromData(imgData)) {
            image.save(destPath, "PNG");
            emit posterReady(itemId, destPath);
        }
    });
}

