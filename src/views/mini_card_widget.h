#pragma once

#include <QWidget>
#include <QPixmap>
#include "../models/media_item.h"

class MiniCardWidget : public QWidget {
    Q_OBJECT
public:
    explicit MiniCardWidget(const MediaItem& item, QWidget* parent = nullptr);

    const MediaItem& getItem() const { return m_item; }

signals:
    void openRequested(const MediaItem& item);

protected:
    void paintEvent(QPaintEvent* event) override;
    void mousePressEvent(QMouseEvent* event) override;

private:
    MediaItem m_item;
    QPixmap m_posterPixmap;
};

