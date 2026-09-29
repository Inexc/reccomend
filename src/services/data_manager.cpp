#include "data_manager.h"
#include <QFile>
#include <QFileInfo>
#include <QDir>
#include <QJsonDocument>
#include <QJsonArray>
#include <QJsonObject>
#include <QCoreApplication>
#include <algorithm>

DataManager::DataManager() {
}

QString DataManager::resolvePath(const QString& relativePath) {
    QString appDir = QCoreApplication::applicationDirPath();
    QStringList candidates = {
        appDir + "/" + relativePath,
        QDir::currentPath() + "/" + relativePath,
        appDir + "/../" + relativePath,
        appDir + "/../../" + relativePath
    };

    for (const QString& cand : candidates) {
        if (QFile::exists(cand)) {
            return QDir::cleanPath(cand);
        }
    }
    // Default to app dir
    return QDir::cleanPath(appDir + "/" + relativePath);
}

void DataManager::load() {
    QString moviesPath = resolvePath("Data/movies.json");
    QString booksPath = resolvePath("Data/books.json");

    m_movies = loadItems(moviesPath, MediaKind::Movie);
    m_books = loadItems(booksPath, MediaKind::Book);
    m_favorites = loadFavorites();
}

QList<MediaItem> DataManager::loadItems(const QString& filePath, MediaKind kind) {
    QList<MediaItem> result;
    QFile file(filePath);
    if (!file.open(QIODevice::ReadOnly | QIODevice::Text)) {
        return result;
    }

    QByteArray data = file.readAll();
    file.close();

    QJsonDocument doc = QJsonDocument::fromJson(data);
    if (!doc.isArray()) {
        return result;
    }

    QJsonArray arr = doc.array();
    for (const auto& val : arr) {
        if (val.isObject()) {
            result.append(MediaItem::fromJson(val.toObject(), kind));
        }
    }
    return result;
}

QSet<QString> DataManager::loadFavorites() {
    QSet<QString> result;
    QString path = resolvePath("Data/favorites.json");
    QFile file(path);
    if (!file.open(QIODevice::ReadOnly | QIODevice::Text)) {
        return result;
    }

    QByteArray data = file.readAll();
    file.close();

    QJsonDocument doc = QJsonDocument::fromJson(data);
    if (doc.isArray()) {
        for (const auto& val : doc.array()) {
            result.insert(val.toString());
        }
    }
    return result;
}

void DataManager::saveFavorites() {
    QString path = resolvePath("Data/favorites.json");
    QFileInfo fi(path);
    QDir().mkpath(fi.dir().absolutePath());

    QFile file(path);
    if (!file.open(QIODevice::WriteOnly | QIODevice::Text)) {
        return;
    }

    QJsonArray arr;
    QStringList sortedList = m_favorites.values();
    std::sort(sortedList.begin(), sortedList.end());
    for (const auto& id : sortedList) {
        arr.append(id);
    }

    QJsonDocument doc(arr);
    file.write(doc.toJson(QJsonDocument::Indented));
    file.close();
}

QList<MediaItem> DataManager::allItems() const {
    QList<MediaItem> all;
    all.reserve(m_movies.size() + m_books.size());
    all.append(m_movies);
    all.append(m_books);
    return all;
}

const MediaItem* DataManager::getById(const QString& id) const {
    for (const auto& item : m_movies) {
        if (item.id == id) return &item;
    }
    for (const auto& item : m_books) {
        if (item.id == id) return &item;
    }
    return nullptr;
}

QStringList DataManager::genresFor(const QList<MediaItem>& items) const {
    QSet<QString> set;
    for (const auto& item : items) {
        for (const auto& g : item.genre) {
            set.insert(g);
        }
    }
    QStringList list = set.values();
    std::sort(list.begin(), list.end());
    return list;
}

QList<MediaItem> DataManager::search(const QString& query, const QList<MediaItem>& pool) const {
    QString q = query.trimmed().toLower();
    if (q.isEmpty()) {
        return pool;
    }

    QList<MediaItem> result;
    for (const auto& item : pool) {
        QString haystack = (item.title + " " + item.author + " " + item.genreText()).toLower();
        if (haystack.contains(q)) {
            result.append(item);
        }
    }
    return result;
}

bool DataManager::isFavorite(const QString& id) const {
    return m_favorites.contains(id);
}

void DataManager::toggleFavorite(const QString& id) {
    if (m_favorites.contains(id)) {
        m_favorites.remove(id);
    } else {
        m_favorites.insert(id);
    }
    saveFavorites();
}

void DataManager::removeFavorite(const QString& id) {
    if (m_favorites.remove(id)) {
        saveFavorites();
    }
}

void DataManager::clearFavorites() {
    m_favorites.clear();
    saveFavorites();
}

QList<MediaItem> DataManager::getFavoriteItems() const {
    QList<MediaItem> result;
    for (const auto& item : allItems()) {
        if (m_favorites.contains(item.id)) {
            result.append(item);
        }
    }
    return result;
}

