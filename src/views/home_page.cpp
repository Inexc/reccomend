#include "home_page.h"
#include "card_widget.h"
#include "main_window.h"
#include "../services/data_manager.h"
#include "../services/poster_service.h"
#include "../services/external_links.h"
#include "../services/recommendation_service.h"
#include "../styles/theme.h"
#include <QPainter>
#include <QPainterPath>
#include <QLinearGradient>
#include <QScrollBar>
#include <QWheelEvent>
#include <QFile>

// Custom horizontal scroll widget that handles mouse wheel for horizontal scroll
class HorizontalScrollArea : public QScrollArea {
public:
    explicit HorizontalScrollArea(QWidget* parent = nullptr) : QScrollArea(parent) {
        setVerticalScrollBarPolicy(Qt::ScrollBarAlwaysOff);
        setHorizontalScrollBarPolicy(Qt::ScrollBarAsNeeded);
        setWidgetResizable(true);
    }
protected:
    void wheelEvent(QWheelEvent* event) override {
        horizontalScrollBar()->setValue(horizontalScrollBar()->value() - event->angleDelta().y());
        event->accept();
    }
};

HomePage::HomePage(DataManager* data, MainWindow* app, QWidget* parent)
    : QWidget(parent), m_data(data), m_app(app) {
    setupUi();
}

void HomePage::setupUi() {
    QVBoxLayout* rootLayout = new QVBoxLayout(this);
    rootLayout->setContentsMargins(0, 0, 0, 0);

    QScrollArea* mainScroll = new QScrollArea(this);
    mainScroll->setWidgetResizable(true);
    mainScroll->setStyleSheet("background: transparent; border: none;");

    QWidget* container = new QWidget();
    container->setStyleSheet("background: transparent;");
    QVBoxLayout* layout = new QVBoxLayout(container);
    layout->setContentsMargins(36, 24, 36, 36);
    layout->setSpacing(28);

    // 1. Search Box
    QWidget* searchContainer = new QWidget();
    searchContainer->setStyleSheet("background-color: #221A1E; border: 1px solid #332A2C; border-radius: 12px;");
    searchContainer->setFixedWidth(380);
    QHBoxLayout* searchLayout = new QHBoxLayout(searchContainer);
    searchLayout->setContentsMargins(14, 4, 14, 4);
    searchLayout->setSpacing(8);

    QLabel* searchIcon = new QLabel("⌕");
    searchIcon->setStyleSheet("font-size: 16px; color: #6E5F5A; border: none; background: transparent;");
    m_searchBox = new QLineEdit();
    m_searchBox->setPlaceholderText("Поиск фильмов, книг, авторов, жанров…");
    m_searchBox->setStyleSheet("border: none; background: transparent; padding: 6px 0; font-size: 13px;");
    connect(m_searchBox, &QLineEdit::returnPressed, this, [this]() {
        QString q = m_searchBox->text().trimmed();
        if (!q.isEmpty()) {
            emit searchRequested(q);
        }
    });

    searchLayout->addWidget(searchIcon);
    searchLayout->addWidget(m_searchBox);
    layout->addWidget(searchContainer);

    // 2. Middle section: Hero (left) + Sidebar widgets (right)
    QWidget* midRow = new QWidget();
    midRow->setStyleSheet("background: transparent;");
    QHBoxLayout* midLayout = new QHBoxLayout(midRow);
    midLayout->setContentsMargins(0, 0, 0, 0);
    midLayout->setSpacing(24);

    // 2A. Hero Frame
    m_heroWidget = new QWidget();
    m_heroWidget->setFixedHeight(400);
    m_heroWidget->setStyleSheet("border-radius: 16px;");

    QVBoxLayout* heroOuterLayout = new QVBoxLayout(m_heroWidget);
    heroOuterLayout->setContentsMargins(36, 36, 36, 36);
    heroOuterLayout->setAlignment(Qt::AlignLeft | Qt::AlignVCenter);
    heroOuterLayout->setSpacing(10);

    // Genres row
    QWidget* genresContainer = new QWidget();
    genresContainer->setStyleSheet("background: transparent;");
    m_heroGenresLayout = new QHBoxLayout(genresContainer);
    m_heroGenresLayout->setContentsMargins(0, 0, 0, 0);
    m_heroGenresLayout->setSpacing(6);
    heroOuterLayout->addWidget(genresContainer);

    // Hero title
    m_heroTitleLabel = new QLabel();
    m_heroTitleLabel->setWordWrap(true);
    m_heroTitleLabel->setStyleSheet("font-size: 30px; font-weight: bold; color: #F2E9E4; background: transparent;");
    heroOuterLayout->addWidget(m_heroTitleLabel);

    // Meta (Author · ★ Rating)
    m_heroMetaLabel = new QLabel();
    m_heroMetaLabel->setStyleSheet("font-size: 13px; color: #D4B483; font-weight: 500; background: transparent;");
    heroOuterLayout->addWidget(m_heroMetaLabel);

    // Description
    m_heroDescLabel = new QLabel();
    m_heroDescLabel->setWordWrap(true);
    m_heroDescLabel->setMaximumWidth(460);
    m_heroDescLabel->setStyleSheet("font-size: 12px; color: #A6968F; line-height: 18px; background: transparent;");
    heroOuterLayout->addWidget(m_heroDescLabel);

    // Buttons
    QWidget* btnRow = new QWidget();
    btnRow->setStyleSheet("background: transparent;");
    QHBoxLayout* btnLayout = new QHBoxLayout(btnRow);
    btnLayout->setContentsMargins(0, 8, 0, 0);
    btnLayout->setSpacing(12);

    m_heroActionBtn = new QPushButton();
    m_heroActionBtn->setObjectName("accentBtn");
    m_heroActionBtn->setCursor(Qt::PointingHandCursor);
    connect(m_heroActionBtn, &QPushButton::clicked, this, [this]() {
        if (m_hasHero) {
            ExternalLinks::open(m_heroItem);
        }
    });

    m_heroFavBtn = new QPushButton();
    m_heroFavBtn->setObjectName("ghostBtn");
    m_heroFavBtn->setCursor(Qt::PointingHandCursor);
    connect(m_heroFavBtn, &QPushButton::clicked, this, [this]() {
        if (m_hasHero) {
            m_data->toggleFavorite(m_heroItem.id);
            updateHeroFavButton();
            buildFavoritesList();
        }
    });

    btnLayout->addWidget(m_heroActionBtn);
    btnLayout->addWidget(m_heroFavBtn);
    btnLayout->addStretch();
    heroOuterLayout->addWidget(btnRow);

    midLayout->addWidget(m_heroWidget, 1);

    // 2B. Right column
    QWidget* rightCol = new QWidget();
    rightCol->setFixedWidth(260);
    rightCol->setStyleSheet("background: transparent;");
    QVBoxLayout* rightColLayout = new QVBoxLayout(rightCol);
    rightColLayout->setContentsMargins(0, 0, 0, 0);
    rightColLayout->setSpacing(16);

    // Favorites box
    QWidget* favsBox = new QWidget();
    favsBox->setStyleSheet("background-color: #221A1E; border: 1px solid #332A2C; border-radius: 12px;");
    QVBoxLayout* favsBoxLayout = new QVBoxLayout(favsBox);
    favsBoxLayout->setContentsMargins(16, 16, 16, 16);
    favsBoxLayout->setSpacing(10);

    QLabel* favsHeader = new QLabel("ИЗБРАННОЕ");
    favsHeader->setStyleSheet("font-size: 11px; font-weight: bold; color: #6E5F5A; letter-spacing: 1px; border: none;");
    favsBoxLayout->addWidget(favsHeader);

    QWidget* favsListWidget = new QWidget();
    favsListWidget->setStyleSheet("border: none;");
    m_favsListLayout = new QVBoxLayout(favsListWidget);
    m_favsListLayout->setContentsMargins(0, 0, 0, 0);
    m_favsListLayout->setSpacing(8);
    favsBoxLayout->addWidget(favsListWidget);

    m_favsEmptyLabel = new QLabel("Добавляйте через ♡");
    m_favsEmptyLabel->setStyleSheet("font-size: 11px; color: #6E5F5A; border: none;");
    favsBoxLayout->addWidget(m_favsEmptyLabel);

    rightColLayout->addWidget(favsBox);

    // Genres box
    QWidget* genresBox = new QWidget();
    genresBox->setStyleSheet("background-color: #221A1E; border: 1px solid #332A2C; border-radius: 12px;");
    QVBoxLayout* genresBoxLayout = new QVBoxLayout(genresBox);
    genresBoxLayout->setContentsMargins(16, 16, 16, 16);
    genresBoxLayout->setSpacing(10);

    QLabel* genresHeader = new QLabel("ЖАНРЫ");
    genresHeader->setStyleSheet("font-size: 11px; font-weight: bold; color: #6E5F5A; letter-spacing: 1px; border: none;");
    genresBoxLayout->addWidget(genresHeader);

    m_genreChipsWidget = new QWidget();
    m_genreChipsWidget->setStyleSheet("border: none;");
    genresBoxLayout->addWidget(m_genreChipsWidget);

    rightColLayout->addWidget(genresBox);
    rightColLayout->addStretch();
    midLayout->addWidget(rightCol);

    layout->addWidget(midRow);

    // 3. Carousel 1: "Рекомендуем для вас"
    QWidget* recsHeaderWidget = new QWidget();
    recsHeaderWidget->setStyleSheet("background: transparent;");
    QHBoxLayout* recsHeaderLayout = new QHBoxLayout(recsHeaderWidget);
    recsHeaderLayout->setContentsMargins(0, 0, 0, 0);

    QVBoxLayout* recsTitleStack = new QVBoxLayout();
    QLabel* recsTitle = new QLabel("Рекомендуем для вас");
    recsTitle->setStyleSheet("font-size: 20px; font-weight: bold; color: #F2E9E4;");
    QLabel* recsSub = new QLabel("Подобрано специально для вас");
    recsSub->setStyleSheet("font-size: 12px; color: #A6968F;");
    recsTitleStack->addWidget(recsTitle);
    recsTitleStack->addWidget(recsSub);
    recsHeaderLayout->addLayout(recsTitleStack);

    QPushButton* recsLeftBtn = new QPushButton("←");
    recsLeftBtn->setObjectName("roundIconBtn");
    recsLeftBtn->setCursor(Qt::PointingHandCursor);
    QPushButton* recsRightBtn = new QPushButton("→");
    recsRightBtn->setObjectName("roundIconBtn");
    recsRightBtn->setCursor(Qt::PointingHandCursor);

    recsHeaderLayout->addWidget(recsLeftBtn);
    recsHeaderLayout->addWidget(recsRightBtn);
    layout->addWidget(recsHeaderWidget);

    m_recsScroll = new HorizontalScrollArea(this);
    m_recsScroll->setFixedHeight(290);
    QWidget* recsContent = new QWidget();
    recsContent->setStyleSheet("background: transparent;");
    m_recsRow = new QHBoxLayout(recsContent);
    m_recsRow->setContentsMargins(0, 0, 0, 0);
    m_recsRow->setSpacing(14);
    m_recsRow->setAlignment(Qt::AlignLeft);
    m_recsScroll->setWidget(recsContent);
    layout->addWidget(m_recsScroll);

    connect(recsLeftBtn, &QPushButton::clicked, this, [this]() {
        m_recsScroll->horizontalScrollBar()->setValue(m_recsScroll->horizontalScrollBar()->value() - 400);
    });
    connect(recsRightBtn, &QPushButton::clicked, this, [this]() {
        m_recsScroll->horizontalScrollBar()->setValue(m_recsScroll->horizontalScrollBar()->value() + 400);
    });

    // 4. Carousel 2: "Новинки"
    QWidget* newHeaderWidget = new QWidget();
    newHeaderWidget->setStyleSheet("background: transparent;");
    QHBoxLayout* newHeaderLayout = new QHBoxLayout(newHeaderWidget);
    newHeaderLayout->setContentsMargins(0, 0, 0, 0);

    QVBoxLayout* newTitleStack = new QVBoxLayout();
    QLabel* newTitle = new QLabel("Новинки");
    newTitle->setStyleSheet("font-size: 20px; font-weight: bold; color: #F2E9E4;");
    QLabel* newSub = new QLabel("Свежее из коллекции");
    newSub->setStyleSheet("font-size: 12px; color: #A6968F;");
    newTitleStack->addWidget(newTitle);
    newTitleStack->addWidget(newSub);
    newHeaderLayout->addLayout(newTitleStack);

    QPushButton* newLeftBtn = new QPushButton("←");
    newLeftBtn->setObjectName("roundIconBtn");
    newLeftBtn->setCursor(Qt::PointingHandCursor);
    QPushButton* newRightBtn = new QPushButton("→");
    newRightBtn->setObjectName("roundIconBtn");
    newRightBtn->setCursor(Qt::PointingHandCursor);

    newHeaderLayout->addWidget(newLeftBtn);
    newHeaderLayout->addWidget(newRightBtn);
    layout->addWidget(newHeaderWidget);

    m_newScroll = new HorizontalScrollArea(this);
    m_newScroll->setFixedHeight(290);
    QWidget* newContent = new QWidget();
    newContent->setStyleSheet("background: transparent;");
    m_newRow = new QHBoxLayout(newContent);
    m_newRow->setContentsMargins(0, 0, 0, 0);
    m_newRow->setSpacing(14);
    m_newRow->setAlignment(Qt::AlignLeft);
    m_newScroll->setWidget(newContent);
    layout->addWidget(m_newScroll);

    connect(newLeftBtn, &QPushButton::clicked, this, [this]() {
        m_newScroll->horizontalScrollBar()->setValue(m_newScroll->horizontalScrollBar()->value() - 400);
    });
    connect(newRightBtn, &QPushButton::clicked, this, [this]() {
        m_newScroll->horizontalScrollBar()->setValue(m_newScroll->horizontalScrollBar()->value() + 400);
    });

    mainScroll->setWidget(container);
    rootLayout->addWidget(mainScroll);

    refresh();
}

void HomePage::setHero(const MediaItem* item) {
    if (!item) return;
    m_heroItem = *item;
    m_hasHero = true;

    // Clear genre chips
    QLayoutItem* child;
    while ((child = m_heroGenresLayout->takeAt(0)) != nullptr) {
        if (child->widget()) child->widget()->deleteLater();
        delete child;
    }

    for (const auto& g : m_heroItem.genre) {
        QLabel* chip = new QLabel(g.toUpper());
        chip->setStyleSheet(
            "background: rgba(22, 17, 20, 200); border: 1px solid #332A2C; border-radius: 10px; "
            "color: #D4B483; font-size: 10px; font-weight: bold; padding: 3px 8px;"
        );
        m_heroGenresLayout->addWidget(chip);
    }
    m_heroGenresLayout->addStretch();

    m_heroTitleLabel->setText(m_heroItem.title);
    m_heroMetaLabel->setText(QString("%1  ·  ★ %2").arg(m_heroItem.author).arg(m_heroItem.rating, 0, 'f', 1));
    m_heroDescLabel->setText(m_heroItem.description);
    m_heroActionBtn->setText(m_heroItem.kind == MediaKind::Movie ? "▷  Смотреть" : "▷  Читать");
    updateHeroFavButton();

    // Set background poster on Hero
    QString posterPath = PosterService::instance().getPosterPath(m_heroItem);
    QPixmap posterPix;
    if (QFile::exists(posterPath)) {
        posterPix.load(posterPath);
    } else {
        PosterService::instance().ensurePoster(m_heroItem);
        if (QFile::exists(posterPath)) {
            posterPix.load(posterPath);
        }
    }

    // Compose styled hero background with dark gradient on top
    if (!posterPix.isNull()) {
        QPixmap heroPix(800, 400);
        heroPix.fill(QColor(22, 17, 20));
        QPainter p(&heroPix);
        p.setRenderHint(QPainter::Antialiasing);

        // Draw poster on the right
        QPixmap scaled = posterPix.scaled(400, 400, Qt::KeepAspectRatioByExpanding, Qt::SmoothTransformation);
        p.drawPixmap(400, 0, scaled);

        // Dark gradient from left to right
        QLinearGradient grad(0, 0, 750, 0);
        grad.setColorAt(0.0, QColor(22, 17, 20, 255));
        grad.setColorAt(0.35, QColor(22, 17, 20, 245));
        grad.setColorAt(0.65, QColor(22, 17, 20, 180));
        grad.setColorAt(1.0, QColor(22, 17, 20, 40));
        p.fillRect(QRect(0, 0, 800, 400), grad);
        p.end();

        m_heroWidget->setStyleSheet(QString(
            "border-radius: 16px; border: 1px solid #332A2C; "
            "background-color: #1C1619;"
        ));
    }
}

void HomePage::updateHeroFavButton() {
    if (!m_hasHero) return;
    bool isFav = m_data->isFavorite(m_heroItem.id);
    m_heroFavBtn->setText(isFav ? "♥  В избранном" : "♡  В избранное");
    if (isFav) {
        m_heroFavBtn->setStyleSheet("QPushButton { border-color: #C1666B; color: #C1666B; }");
    } else {
        m_heroFavBtn->setStyleSheet("");
    }
}

void HomePage::buildFavoritesList() {
    QLayoutItem* child;
    while ((child = m_favsListLayout->takeAt(0)) != nullptr) {
        if (child->widget()) child->widget()->deleteLater();
        delete child;
    }

    QList<MediaItem> favs = m_data->getFavoriteItems();
    int count = std::min(4, static_cast<int>(favs.size()));
    m_favsEmptyLabel->setVisible(count == 0);

    for (int i = 0; i < count; ++i) {
        const auto& item = favs[i];
        QWidget* row = new QWidget();
        row->setCursor(Qt::PointingHandCursor);
        row->setStyleSheet("border: none; background: transparent;");
        QHBoxLayout* rowLayout = new QHBoxLayout(row);
        rowLayout->setContentsMargins(0, 0, 0, 0);
        rowLayout->setSpacing(10);

        QLabel* poster = new QLabel();
        poster->setFixedSize(36, 48);
        poster->setScaledContents(true);
        poster->setStyleSheet("border-radius: 4px; background-color: #161114;");
        QString path = PosterService::instance().getPosterPath(item);
        if (QFile::exists(path)) {
            poster->setPixmap(QPixmap(path));
        }
        rowLayout->addWidget(poster);

        QVBoxLayout* textLayout = new QVBoxLayout();
        textLayout->setSpacing(2);
        QLabel* title = new QLabel(item.title);
        title->setStyleSheet("font-size: 11px; font-weight: bold; color: #F2E9E4; border: none;");
        QLabel* sub = new QLabel(item.author.isEmpty() ? item.genreText() : item.author);
        sub->setStyleSheet("font-size: 10px; color: #6E5F5A; border: none;");
        textLayout->addWidget(title);
        textLayout->addWidget(sub);
        rowLayout->addLayout(textLayout, 1);

        // Click row to set Hero and open details
        row->installEventFilter(this);
        QPushButton* overlayBtn = new QPushButton(row);
        overlayBtn->setGeometry(0, 0, 240, 50);
        overlayBtn->setStyleSheet("background: transparent; border: none;");
        overlayBtn->setCursor(Qt::PointingHandCursor);
        connect(overlayBtn, &QPushButton::clicked, this, [this, item]() {
            setHero(&item);
            emit openDetailsRequested(item);
        });

        m_favsListLayout->addWidget(row);
    }
}

void HomePage::buildGenreChips() {
    // Clear chips
    QLayout* oldLayout = m_genreChipsWidget->layout();
    if (oldLayout) {
        QLayoutItem* item;
        while ((item = oldLayout->takeAt(0)) != nullptr) {
            if (item->widget()) item->widget()->deleteLater();
            delete item;
        }
        delete oldLayout;
    }

    QVBoxLayout* vbox = new QVBoxLayout(m_genreChipsWidget);
    vbox->setContentsMargins(0, 0, 0, 0);
    vbox->setSpacing(6);

    QHBoxLayout* row1 = new QHBoxLayout();
    QHBoxLayout* row2 = new QHBoxLayout();
    row1->setSpacing(6);
    row2->setSpacing(6);

    QStringList genres = m_data->genresFor(m_data->allItems());
    int limit = std::min(8, static_cast<int>(genres.size()));
    for (int i = 0; i < limit; ++i) {
        QString g = genres[i];
        QPushButton* chip = new QPushButton(g);
        chip->setObjectName("chipBtn");
        chip->setCursor(Qt::PointingHandCursor);
        connect(chip, &QPushButton::clicked, this, [this, g]() {
            emit genreSelected(g);
        });
        if (i < 4) {
            row1->addWidget(chip);
        } else {
            row2->addWidget(chip);
        }
    }
    row1->addStretch();
    row2->addStretch();
    vbox->addLayout(row1);
    vbox->addLayout(row2);
}

void HomePage::buildCarousel(QHBoxLayout* row, const QList<MediaItem>& items) {
    QLayoutItem* child;
    while ((child = row->takeAt(0)) != nullptr) {
        if (child->widget()) child->widget()->deleteLater();
        delete child;
    }

    for (const auto& item : items) {
        CardWidget* card = new CardWidget(item, m_data, this);
        connect(card, &CardWidget::openRequested, this, [this](const MediaItem& it) {
            setHero(&it);
            emit openDetailsRequested(it);
        });
        connect(card, &CardWidget::favoriteChanged, this, [this](const MediaItem&) {
            buildFavoritesList();
            updateHeroFavButton();
        });
        row->addWidget(card);
    }
}

void HomePage::refresh() {
    MediaItem featured = RecommendationService::pickFeatured(m_data->allItems(), m_data->getFavorites());
    if (!featured.id.isEmpty()) {
        setHero(&featured);
    }
    buildFavoritesList();
    buildGenreChips();

    // Top rated pool
    QList<MediaItem> all = m_data->allItems();
    std::sort(all.begin(), all.end(), [](const MediaItem& a, const MediaItem& b) {
        return a.rating > b.rating;
    });
    QList<MediaItem> topRated;
    for (int i = 0; i < std::min(12, static_cast<int>(all.size())); ++i) {
        topRated.append(all[i]);
    }
    buildCarousel(m_recsRow, topRated);

    // New releases (by year descending)
    QList<MediaItem> byYear = m_data->allItems();
    std::sort(byYear.begin(), byYear.end(), [](const MediaItem& a, const MediaItem& b) {
        return a.year > b.year;
    });
    QList<MediaItem> newest;
    for (int i = 0; i < std::min(12, static_cast<int>(byYear.size())); ++i) {
        newest.append(byYear[i]);
    }
    buildCarousel(m_newRow, newest);
}

