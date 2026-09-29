#pragma once

#include <QObject>
#include <QString>
#include <QNetworkAccessManager>
#include <QNetworkReply>
#include <QPixmap>
#include "../models/media_item.h"

class PosterService : public QObject {
    Q_OBJECT
public:
    explicit PosterService(QObject* parent = nullptr);
    static PosterService& instance();

    QString getPosterPath(const MediaItem& item);
    void ensurePoster(const MediaItem& item);
    static void generatePlaceholder(const MediaItem& item, const QString& destPath);

signals:
    void posterReady(const QString& itemId, const QString& filePath);

private:
    void fetchMoviePoster(const MediaItem& item, const QString& destPath);
    void fetchBookCover(const MediaItem& item, const QString& destPath);
    void downloadImage(const QString& url, const QString& destPath, const QString& itemId);

    QNetworkAccessManager* m_nam;
};

