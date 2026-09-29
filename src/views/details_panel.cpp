#include "details_panel.h"
#include "mini_card_widget.h"
#include "../services/data_manager.h"
#include "../services/poster_service.h"
#include "../services/external_links.h"
#include "../services/recommendation_service.h"
#include "../styles/theme.h"
#include <QPainter>
#include <QPainterPath>
#include <QFile>

DetailsPanel::DetailsPanel(DataManager* data, QWidget* parent)
    : QWidget(parent), m_data(data) {
    setFixedWidth(290);
    setStyleSheet("background-color: #141013; border-left: 1px solid #332A2C;");
    setupUi();
}

void DetailsPanel::setupUi() {
    QVBoxLayout* rootLayout = new QVBoxLayout(this);
    rootLayout->setContentsMargins(0, 0, 0, 0);

    QScrollArea* scroll = new QScrollArea(this);
    scroll->setWidgetResizable(true);
    scroll->setStyleSheet("background: transparent; border: none;");

    m_contentWidget = new QWidget();
    m_contentWidget->setStyleSheet("background: transparent; border: none;");
    m_contentLayout = new QVBoxLayout(m_contentWidget);
    m_contentLayout->setContentsMargins(18, 20, 18, 24);
    m_contentLayout->setSpacing(10);

    // Poster
    m_posterLabel = new QLabel();
    m_posterLabel->setFixedHeight(220);
    m_posterLabel->setScaledContents(true);
    m_posterLabel->setStyleSheet("border-radius: 10px; background-color: #221A1E; border: 1px solid #332A2C;");
    m_contentLayout->addWidget(m_posterLabel);

    // Title
    m_titleLabel = new QLabel();
    m_titleLabel->setWordWrap(true);
    m_titleLabel->setStyleSheet("font-size: 16px; font-weight: bold; color: #F2E9E4; border: none;");
    m_contentLayout->addWidget(m_titleLabel);

    // Genre
    m_genreLabel = new QLabel();
    m_genreLabel->setWordWrap(true);
    m_genreLabel->setStyleSheet("font-size: 11px; color: #A6968F; border: none;");
    m_contentLayout->addWidget(m_genreLabel);

    // Rating
    m_ratingLabel = new QLabel();
    m_ratingLabel->setStyleSheet("font-size: 12px; font-weight: bold; color: #D4B483; border: none;");
    m_contentLayout->addWidget(m_ratingLabel);

    // Description
    m_descLabel = new QLabel();
    m_descLabel->setWordWrap(true);
    m_descLabel->setStyleSheet("font-size: 11px; color: #A6968F; line-height: 16px; border: none;");
    m_contentLayout->addWidget(m_descLabel);

    // Action button ("▷ Смотреть" / "▷ Читать")
    m_actionBtn = new QPushButton();
    m_actionBtn->setObjectName("accentBtn");
    m_actionBtn->setCursor(Qt::PointingHandCursor);
    connect(m_actionBtn, &QPushButton::clicked, this, [this]() {
        if (!m_cachedItem.id.isEmpty()) {
            ExternalLinks::open(m_cachedItem);
        }
    });
    m_contentLayout->addWidget(m_actionBtn);

    // Favorite button
    m_favBtn = new QPushButton();
    m_favBtn->setObjectName("ghostBtn");
    m_favBtn->setCursor(Qt::PointingHandCursor);
    connect(m_favBtn, &QPushButton::clicked, this, [this]() {
        if (!m_cachedItem.id.isEmpty()) {
            m_data->toggleFavorite(m_cachedItem.id);
            updateFavoriteState();
            emit favoriteChanged(m_cachedItem);
        }
    });
    m_contentLayout->addWidget(m_favBtn);

    // Reasons to watch
    m_reasonsHeader = new QLabel("Почему стоит оценить?");
    m_reasonsHeader->setStyleSheet("font-size: 12px; font-weight: bold; color: #F2E9E4; margin-top: 10px; border: none;");
    m_contentLayout->addWidget(m_reasonsHeader);

    m_reasonsWidget = new QWidget();
    m_reasonsWidget->setStyleSheet("border: none;");
    QVBoxLayout* reasonsLayout = new QVBoxLayout(m_reasonsWidget);
    reasonsLayout->setContentsMargins(0, 0, 0, 0);
    reasonsLayout->setSpacing(4);

    const QStringList reasons = { "✓  Уникальный сюжет", "✓  Глубокие эмоции", "✓  Интересные персонажи" };
    for (const auto& r : reasons) {
        QLabel* l = new QLabel(r);
        l->setStyleSheet("font-size: 11px; color: #A6968F; border: none;");
        reasonsLayout->addWidget(l);
    }
    m_contentLayout->addWidget(m_reasonsWidget);

    // Similar section
    m_similarHeader = new QLabel("Похожие");
    m_similarHeader->setStyleSheet("font-size: 12px; font-weight: bold; color: #F2E9E4; margin-top: 10px; border: none;");
    m_contentLayout->addWidget(m_similarHeader);

    QWidget* similarWidget = new QWidget();
    similarWidget->setStyleSheet("border: none;");
    m_similarLayout = new QHBoxLayout(similarWidget);
    m_similarLayout->setContentsMargins(0, 0, 0, 0);
    m_similarLayout->setSpacing(8);
    m_contentLayout->addWidget(similarWidget);

    m_contentLayout->addStretch();
    scroll->setWidget(m_contentWidget);
    rootLayout->addWidget(scroll);
}

void DetailsPanel::showItem(const MediaItem* item) {
    m_currentItem = item;
    if (!item) {
        m_posterLabel->setVisible(false);
        m_titleLabel->setText("Выберите произведение,\nчтобы увидеть подробности.");
        m_genreLabel->setVisible(false);
        m_ratingLabel->setVisible(false);
        m_descLabel->setVisible(false);
        m_actionBtn->setVisible(false);
        m_favBtn->setVisible(false);
        m_reasonsHeader->setVisible(false);
        m_reasonsWidget->setVisible(false);
        m_similarHeader->setVisible(false);
        return;
    }

    m_cachedItem = *item;
    m_posterLabel->setVisible(true);
    m_genreLabel->setVisible(true);
    m_ratingLabel->setVisible(true);
    m_descLabel->setVisible(true);
    m_actionBtn->setVisible(true);
    m_favBtn->setVisible(true);
    m_reasonsHeader->setVisible(true);
    m_reasonsWidget->setVisible(true);
    m_similarHeader->setVisible(true);

    // Poster
    QString posterPath = PosterService::instance().getPosterPath(*item);
    QPixmap pix;
    if (QFile::exists(posterPath)) {
        pix.load(posterPath);
    } else {
        PosterService::instance().ensurePoster(*item);
        if (QFile::exists(posterPath)) {
            pix.load(posterPath);
        }
    }

    if (!pix.isNull()) {
        QPixmap rounded(m_posterLabel->width(), 220);
        rounded.fill(Qt::transparent);
        QPainter p(&rounded);
        p.setRenderHint(QPainter::Antialiasing);
        QPainterPath clip;
        clip.addRoundedRect(0, 0, m_posterLabel->width(), 220, 10, 10);
        p.setClipPath(clip);
        p.drawPixmap(0, 0, pix.scaled(m_posterLabel->width(), 220, Qt::KeepAspectRatioByExpanding, Qt::SmoothTransformation));
        p.end();
        m_posterLabel->setPixmap(rounded);
    }

    m_titleLabel->setText(item->title);
    m_genreLabel->setText(item->genreText());
    m_ratingLabel->setText(QString("⭐ %1 / 10").arg(item->rating, 0, 'f', 1));
    m_descLabel->setText(item->description);

    m_actionBtn->setText(item->kind == MediaKind::Movie ? "▷  Смотреть" : "▷  Читать");
    updateFavoriteState();

    // Similar items
    // Clear previous
    QLayoutItem* child;
    while ((child = m_similarLayout->takeAt(0)) != nullptr) {
        if (child->widget()) {
            child->widget()->deleteLater();
        }
        delete child;
    }

    QList<MediaItem> pool = m_data->allItems();
    QList<MediaItem> similar = RecommendationService::recommendSimilar(*item, pool, 3);
    for (const auto& sim : similar) {
        MiniCardWidget* mini = new MiniCardWidget(sim, this);
        connect(mini, &MiniCardWidget::openRequested, this, &DetailsPanel::openRequested);
        m_similarLayout->addWidget(mini);
    }
}

void DetailsPanel::updateFavoriteState() {
    if (m_cachedItem.id.isEmpty()) return;
    bool isFav = m_data->isFavorite(m_cachedItem.id);
    m_favBtn->setText(isFav ? "♥  В избранном" : "♡  Добавить в избранное");
    if (isFav) {
        m_favBtn->setStyleSheet("QPushButton { border-color: #C1666B; color: #C1666B; }");
    } else {
        m_favBtn->setStyleSheet("");
    }
}

