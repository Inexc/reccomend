#pragma once

#include <QString>
#include <QStringList>
#include <QJsonObject>
#include <QJsonArray>

enum class MediaKind {
    Movie,
    Book
};

struct MediaItem {
    QString id;
    QString title;
    QString author;
    QStringList genre;
    int year = 0;
    double rating = 0.0;
    QString description;
    QString poster;
    MediaKind kind = MediaKind::Movie;

    QString genreText() const {
        return genre.join(", ");
    }

    static MediaItem fromJson(const QJsonObject& obj, MediaKind kind) {
        MediaItem item;
        item.id = obj.value("id").toString();
        item.title = obj.value("title").toString();
        item.author = obj.value("author").toString();
        
        QJsonArray genreArr = obj.value("genre").toArray();
        for (const auto& g : genreArr) {
            item.genre.append(g.toString());
        }

        item.year = obj.value("year").toInt();
        item.rating = obj.value("rating").toDouble();
        item.description = obj.value("description").toString();
        item.poster = obj.value("poster").toString();
        item.kind = kind;
        return item;
    }

    QJsonObject toJson() const {
        QJsonObject obj;
        obj["id"] = id;
        obj["title"] = title;
        obj["author"] = author;
        QJsonArray genreArr;
        for (const auto& g : genre) {
            genreArr.append(g);
        }
        obj["genre"] = genreArr;
        obj["year"] = year;
        obj["rating"] = rating;
        obj["description"] = description;
        obj["poster"] = poster;
        return obj;
    }
};

