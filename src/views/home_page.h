#pragma once

#include <QWidget>
#include <QLineEdit>
#include <QLabel>
#include <QPushButton>
#include <QScrollArea>
#include <QHBoxLayout>
#include <QVBoxLayout>
#include "../models/media_item.h"

class DataManager;
class MainWindow;

class HomePage : public QWidget {
    Q_OBJECT
public:
    explicit HomePage(DataManager* data, MainWindow* app, QWidget* parent = nullptr);

    void refresh();
    void setHero(const MediaItem* item);

signals:
    void openDetailsRequested(const MediaItem& item);
    void searchRequested(const QString& query);
    void genreSelected(const QString& genre);

private:
    void setupUi();
    void buildFavoritesList();
    void buildGenreChips();
    void buildCarousel(QHBoxLayout* row, const QList<MediaItem>& items);
    void updateHeroFavButton();

    DataManager* m_data;
    MainWindow* m_app;
    MediaItem m_heroItem;
    bool m_hasHero = false;

    QLineEdit* m_searchBox;
    QWidget* m_heroWidget;
    QLabel* m_heroPosterLabel;
    QLabel* m_heroTitleLabel;
    QLabel* m_heroMetaLabel;
    QLabel* m_heroDescLabel;
    QPushButton* m_heroActionBtn;
    QPushButton* m_heroFavBtn;
    QHBoxLayout* m_heroGenresLayout;

    QVBoxLayout* m_favsListLayout;
    QLabel* m_favsEmptyLabel;
    QWidget* m_genreChipsWidget;
    QScrollArea* m_recsScroll;
    QHBoxLayout* m_recsRow;
    QScrollArea* m_newScroll;
    QHBoxLayout* m_newRow;
};

