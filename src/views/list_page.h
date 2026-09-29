#pragma once

#include <QWidget>
#include <QLabel>
#include <QLineEdit>
#include <QComboBox>
#include <QScrollArea>
#include <QGridLayout>
#include <functional>
#include "../models/media_item.h"

class DataManager;
class MainWindow;

class ListPage : public QWidget {
    Q_OBJECT
public:
    explicit ListPage(DataManager* data, MainWindow* app,
                      std::function<QList<MediaItem>()> itemsGetter,
                      const QString& title, const QString& icon,
                      QWidget* parent = nullptr);

    void refresh();
    void setSearchQuery(const QString& query);
    void setGenreFilter(const QString& genre);

signals:
    void openDetailsRequested(const MediaItem& item);
    void favoriteChanged(const MediaItem& item);

protected:
    void resizeEvent(QResizeEvent* event) override;

private:
    void setupUi();
    void relayoutCards();

    DataManager* m_data;
    MainWindow* m_app;
    std::function<QList<MediaItem>()> m_itemsGetter;
    QString m_title;
    QString m_icon;
    bool m_suppressEvents = false;

    QLabel* m_titleLabel;
    QLabel* m_subtitleLabel;
    QLineEdit* m_searchBox;
    QComboBox* m_genreCombo;
    QComboBox* m_sortCombo;
    QScrollArea* m_scrollArea;
    QWidget* m_cardsContainer;
    QGridLayout* m_cardsLayout;
    QLabel* m_emptyLabel;

    QList<MediaItem> m_currentItems;
};

