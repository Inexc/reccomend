#pragma once

#include <QMainWindow>
#include <QStackedWidget>
#include <QMap>
#include "../services/data_manager.h"
#include "../models/media_item.h"

class Sidebar;
class DetailsPanel;
class HomePage;
class ListPage;
class FavoritesPage;
class SettingsPage;

class MainWindow : public QMainWindow {
    Q_OBJECT
public:
    explicit MainWindow(QWidget* parent = nullptr);

    void showPage(const QString& key, const QString& query = "", const QString& genreFilter = "");
    void openItem(const MediaItem& item);
    void showFeaturedDefault();
    void onFavoriteChanged(const MediaItem& item);

private:
    void setupUi();

    DataManager m_data;

    Sidebar* m_sidebar;
    QStackedWidget* m_pageStack;
    DetailsPanel* m_detailsPanel;

    HomePage* m_homePage;
    ListPage* m_moviesPage;
    ListPage* m_booksPage;
    FavoritesPage* m_favoritesPage;
    SettingsPage* m_settingsPage;

    QMap<QString, int> m_pageIndices;
    QString m_currentPageKey = "home";
};

