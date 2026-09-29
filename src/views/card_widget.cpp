#include "card_widget.h"
#include "../services/data_manager.h"
#include "../services/poster_service.h"
#include "../styles/theme.h"
#include <QPainter>
#include <QPainterPath>
#include <QLinearGradient>
#include <QPushButton>
#include <QFontMetrics>
#include <QMouseEvent>
#include <QFile>

CardWidget::CardWidget(const MediaItem& item, DataManager* data, QWidget* parent)
    : QWidget(parent), m_item(item), m_data(data) {
    setCursor(Qt::PointingHandCursor);
    setMouseTracking(true);
    setFixedSize(188, 270);

    m_favBtn = new QPushButton(this);
    m_favBtn->setFixedSize(30, 30);
    m_favBtn->setCursor(Qt::PointingHandCursor);
    m_favBtn->move(width() - 38, 8);
    
    connect(m_favBtn, &QPushButton::clicked, this, [this]() {
        m_data->toggleFavorite(m_item.id);
        updateFavoriteState();
        emit favoriteChanged(m_item);
    });

    updateFavoriteState();
    loadPoster();

    connect(&PosterService::instance(), &PosterService::posterReady, this, [this](const QString& id, const QString&) {
        if (id == m_item.id) {
            loadPoster();
            update();
        }
    });
}

void CardWidget::loadPoster() {
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

void CardWidget::updateFavoriteState() {
    bool isFav = m_data->isFavorite(m_item.id);
    m_favBtn->setText(isFav ? "♥" : "♡");
    if (isFav) {
        m_favBtn->setStyleSheet(
            "QPushButton { background: rgba(34, 26, 30, 180); border: 1px solid #C1666B; "
            "border-radius: 15px; color: #C1666B; font-size: 16px; font-weight: bold; } "
            "QPushButton:hover { background: #C1666B; color: #FFFFFF; }"
        );
    } else {
        m_favBtn->setStyleSheet(
            "QPushButton { background: rgba(34, 26, 30, 160); border: 1px solid #332A2C; "
            "border-radius: 15px; color: #F2E9E4; font-size: 15px; } "
            "QPushButton:hover { border-color: #C1666B; color: #C1666B; }"
        );
    }
}

void CardWidget::resizeEvent(QResizeEvent* event) {
    QWidget::resizeEvent(event);
    m_favBtn->move(width() - 38, 8);
}

void CardWidget::enterEvent(QEnterEvent* event) {
    QWidget::enterEvent(event);
    m_isHovered = true;
    update();
}

void CardWidget::leaveEvent(QEvent* event) {
    QWidget::leaveEvent(event);
    m_isHovered = false;
    update();
}

void CardWidget::mousePressEvent(QMouseEvent* event) {
    if (event->button() == Qt::LeftButton) {
        emit openRequested(m_item);
    }
    QWidget::mousePressEvent(event);
}

void CardWidget::paintEvent(QPaintEvent*) {
    QPainter painter(this);
    painter.setRenderHint(QPainter::Antialiasing);
    painter.setRenderHint(QPainter::TextAntialiasing);

    QRect r = rect().adjusted(1, 1, -1, -1);
    QPainterPath clipPath;
    clipPath.addRoundedRect(r, 12, 12);
    painter.setClipPath(clipPath);

    // Draw background / poster
    if (!m_posterPixmap.isNull()) {
        QPixmap scaled = m_posterPixmap.scaled(r.size(), Qt::KeepAspectRatioByExpanding, Qt::SmoothTransformation);
        int sx = (scaled.width() - r.width()) / 2;
        int sy = (scaled.height() - r.height()) / 2;
        painter.drawPixmap(r.topLeft(), scaled, QRect(sx, sy, r.width(), r.height()));
    } else {
        painter.fillRect(r, Theme::Card);
    }

    // Hover brightening
    if (m_isHovered) {
        painter.fillRect(r, QColor(255, 255, 255, 15));
    }

    // Gradient overlay at bottom
    int gradHeight = 130;
    QLinearGradient grad(0, r.bottom() - gradHeight, 0, r.bottom());
    grad.setColorAt(0.0, QColor(22, 17, 20, 0));
    grad.setColorAt(0.4, QColor(22, 17, 20, 160));
    grad.setColorAt(0.7, QColor(22, 17, 20, 220));
    grad.setColorAt(1.0, QColor(22, 17, 20, 250));
    painter.fillRect(QRect(r.left(), r.bottom() - gradHeight, r.width(), gradHeight), grad);

    // Draw text
    int padX = 12;
    int textBottom = r.bottom() - 10;

    // Subtitle (Author or Genre)
    QString sub = m_item.author.isEmpty() ? m_item.genreText() : m_item.author;
    QFont subFont("Segoe UI", 9);
    painter.setFont(subFont);
    painter.setPen(Theme::TextMuted);
    QFontMetrics fmSub(subFont);
    QString elidedSub = fmSub.elidedText(sub, Qt::ElideRight, r.width() - padX * 2);
    painter.drawText(r.left() + padX, textBottom, elidedSub);

    // Title
    int titleBottom = textBottom - 18;
    QFont titleFont("Segoe UI", 11, QFont::DemiBold);
    painter.setFont(titleFont);
    painter.setPen(Theme::Text);
    QFontMetrics fmTitle(titleFont);
    QString elidedTitle = fmTitle.elidedText(m_item.title, Qt::ElideRight, r.width() - padX * 2);
    painter.drawText(r.left() + padX, titleBottom, elidedTitle);

    // Rating
    int ratingBottom = titleBottom - 18;
    QFont ratingFont("Segoe UI", 9, QFont::Bold);
    painter.setFont(ratingFont);
    painter.setPen(Theme::AccentGold);
    painter.drawText(r.left() + padX, ratingBottom, QString("★ %1").arg(m_item.rating, 0, 'f', 1));

    // Outer border
    painter.setClipping(false);
    QPen borderPen(m_isHovered ? Theme::Accent : Theme::Border, m_isHovered ? 2.0 : 1.0);
    painter.setPen(borderPen);
    painter.setBrush(Qt::NoBrush);
    painter.drawRoundedRect(r, 12, 12);
}

