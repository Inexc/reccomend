#include "mini_card_widget.h"
#include "../services/poster_service.h"
#include "../styles/theme.h"
#include <QPainter>
#include <QPainterPath>
#include <QFontMetrics>
#include <QMouseEvent>
#include <QFile>

MiniCardWidget::MiniCardWidget(const MediaItem& item, QWidget* parent)
    : QWidget(parent), m_item(item) {
    setCursor(Qt::PointingHandCursor);
    setFixedSize(72, 115);

    QString path = PosterService::instance().getPosterPath(m_item);
    if (QFile::exists(path)) {
        m_posterPixmap.load(path);
    } else {
        PosterService::instance().ensurePoster(m_item);
        if (QFile::exists(path)) {
            m_posterPixmap.load(path);
        }
    }
}

void MiniCardWidget::mousePressEvent(QMouseEvent* event) {
    if (event->button() == Qt::LeftButton) {
        emit openRequested(m_item);
    }
    QWidget::mousePressEvent(event);
}

void MiniCardWidget::paintEvent(QPaintEvent*) {
    QPainter painter(this);
    painter.setRenderHint(QPainter::Antialiasing);
    painter.setRenderHint(QPainter::TextAntialiasing);

    QRect imgRect(0, 0, width(), 92);
    QPainterPath path;
    path.addRoundedRect(imgRect, 8, 8);
    painter.setClipPath(path);

    if (!m_posterPixmap.isNull()) {
        QPixmap scaled = m_posterPixmap.scaled(imgRect.size(), Qt::KeepAspectRatioByExpanding, Qt::SmoothTransformation);
        int sx = (scaled.width() - imgRect.width()) / 2;
        int sy = (scaled.height() - imgRect.height()) / 2;
        painter.drawPixmap(imgRect.topLeft(), scaled, QRect(sx, sy, imgRect.width(), imgRect.height()));
    } else {
        painter.fillRect(imgRect, Theme::Card);
    }

    painter.setClipping(false);
    painter.setPen(Theme::Border);
    painter.drawRoundedRect(imgRect, 8, 8);

    QFont font("Segoe UI", 8);
    painter.setFont(font);
    painter.setPen(Theme::Text);
    QFontMetrics fm(font);
    QString elided = fm.elidedText(m_item.title, Qt::ElideRight, width());
    painter.drawText(QRect(0, 96, width(), 16), Qt::AlignCenter, elided);
}

