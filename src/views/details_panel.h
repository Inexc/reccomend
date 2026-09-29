#pragma once

#include <QWidget>
#include <QLabel>
#include <QPushButton>
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QScrollArea>
#include "../models/media_item.h"

class DataManager;

class DetailsPanel : public QWidget {
    Q_OBJECT
public:
    explicit DetailsPanel(DataManager* data, QWidget* parent = nullptr);

    void showItem(const MediaItem* item);
    const MediaItem* currentItem() const { return m_currentItem; }
    void updateFavoriteState();

signals:
    void openRequested(const MediaItem& item);
    void favoriteChanged(const MediaItem& item);

private:
    void setupUi();

    DataManager* m_data;
    const MediaItem* m_currentItem = nullptr;
    MediaItem m_cachedItem; // Store copy to be safe

    QWidget* m_contentWidget;
    QVBoxLayout* m_contentLayout;
    QLabel* m_posterLabel;
    QLabel* m_titleLabel;
    QLabel* m_genreLabel;
    QLabel* m_ratingLabel;
    QLabel* m_descLabel;
    QPushButton* m_actionBtn;
    QPushButton* m_favBtn;
    QLabel* m_reasonsHeader;
    QWidget* m_reasonsWidget;
    QLabel* m_similarHeader;
    QHBoxLayout* m_similarLayout;
};

