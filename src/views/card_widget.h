#pragma once

#include <QWidget>
#include <QPixmap>
#include "../models/media_item.h"

class DataManager;
class QLabel;
class QPushButton;

class CardWidget : public QWidget {
    Q_OBJECT
public:
    explicit CardWidget(const MediaItem& item, DataManager* data, QWidget* parent = nullptr);

    const MediaItem& getItem() const { return m_item; }
    void updateFavoriteState();

signals:
    void openRequested(const MediaItem& item);
    void favoriteChanged(const MediaItem& item);

protected:
    void paintEvent(QPaintEvent* event) override;
    void enterEvent(QEnterEvent* event) override;
    void leaveEvent(QEvent* event) override;
    void mousePressEvent(QMouseEvent* event) override;
    void resizeEvent(QResizeEvent* event) override;

private:
    void loadPoster();

    MediaItem m_item;
    DataManager* m_data;
    QPixmap m_posterPixmap;
    bool m_isHovered = false;
    QPushButton* m_favBtn;
};

