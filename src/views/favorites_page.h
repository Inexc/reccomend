#pragma once

#include <QWidget>
#include <QLabel>
#include <QPushButton>
#include <QScrollArea>
#include <QGridLayout>
#include "../models/media_item.h"

class DataManager;
class MainWindow;

class FavoritesPage : public QWidget {
    Q_OBJECT
public:
    explicit FavoritesPage(DataManager* data, MainWindow* app, QWidget* parent = nullptr);

    void refresh();

signals:
    void openDetailsRequested(const MediaItem& item);
    void favoriteChanged(const MediaItem& item);

protected:
    void resizeEvent(QResizeEvent* event) override;

private:
    void setupUi();
    void relayoutCards();
    void updateFilterButtons();

    DataManager* m_data;
    MainWindow* m_app;
    QString m_currentFilter = "all"; // all, movies, books

    QLabel* m_statsLabel;
    QPushButton* m_filterAllBtn;
    QPushButton* m_filterMoviesBtn;
    QPushButton* m_filterBooksBtn;
    QScrollArea* m_scrollArea;
    QWidget* m_cardsContainer;
    QGridLayout* m_cardsLayout;
    QWidget* m_emptyStateWidget;

    QList<MediaItem> m_filteredItems;
};

