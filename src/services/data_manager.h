#pragma once

#include <QString>
#include <QList>
#include <QSet>
#include "../models/media_item.h"

class DataManager {
public:
    DataManager();

    void load();
    void saveFavorites();

    const QList<MediaItem>& getMovies() const { return m_movies; }
    const QList<MediaItem>& getBooks() const { return m_books; }
    const QSet<QString>& getFavorites() const { return m_favorites; }

    QList<MediaItem> allItems() const;
    const MediaItem* getById(const QString& id) const;

    QStringList genresFor(const QList<MediaItem>& items) const;
    QList<MediaItem> search(const QString& query, const QList<MediaItem>& pool) const;

    bool isFavorite(const QString& id) const;
    void toggleFavorite(const QString& id);
    void removeFavorite(const QString& id);
    void clearFavorites();
    QList<MediaItem> getFavoriteItems() const;

    static QString resolvePath(const QString& relativePath);

private:
    QList<MediaItem> loadItems(const QString& filePath, MediaKind kind);
    QSet<QString> loadFavorites();

    QList<MediaItem> m_movies;
    QList<MediaItem> m_books;
    QSet<QString> m_favorites;
};

