#include "favorites_page.h"
#include "card_widget.h"
#include "main_window.h"
#include "../services/data_manager.h"
#include "../styles/theme.h"
#include <QVBoxLayout>
#include <QHBoxLayout>

FavoritesPage::FavoritesPage(DataManager* data, MainWindow* app, QWidget* parent)
    : QWidget(parent), m_data(data), m_app(app) {
    setupUi();
}

void FavoritesPage::setupUi() {
    QVBoxLayout* rootLayout = new QVBoxLayout(this);
    rootLayout->setContentsMargins(36, 28, 36, 28);
    rootLayout->setSpacing(20);

    // Title
    QLabel* titleLabel = new QLabel("Избранное");
    titleLabel->setStyleSheet("font-size: 26px; font-weight: bold; color: #F2E9E4;");
    rootLayout->addWidget(titleLabel);

    // Stats bar + filters
    QWidget* topBar = new QWidget();
    topBar->setStyleSheet("background: transparent;");
    QHBoxLayout* topLayout = new QHBoxLayout(topBar);
    topLayout->setContentsMargins(0, 0, 0, 0);
    topLayout->setSpacing(12);

    m_filterAllBtn = new QPushButton("Все");
    m_filterAllBtn->setObjectName("chipBtn");
    m_filterAllBtn->setCursor(Qt::PointingHandCursor);

    m_filterMoviesBtn = new QPushButton("Фильмы");
    m_filterMoviesBtn->setObjectName("chipBtn");
    m_filterMoviesBtn->setCursor(Qt::PointingHandCursor);

    m_filterBooksBtn = new QPushButton("Книги");
    m_filterBooksBtn->setObjectName("chipBtn");
    m_filterBooksBtn->setCursor(Qt::PointingHandCursor);

    connect(m_filterAllBtn, &QPushButton::clicked, this, [this]() {
        m_currentFilter = "all";
        updateFilterButtons();
        refresh();
    });
    connect(m_filterMoviesBtn, &QPushButton::clicked, this, [this]() {
        m_currentFilter = "movies";
        updateFilterButtons();
        refresh();
    });
    connect(m_filterBooksBtn, &QPushButton::clicked, this, [this]() {
        m_currentFilter = "books";
        updateFilterButtons();
        refresh();
    });

    topLayout->addWidget(m_filterAllBtn);
    topLayout->addWidget(m_filterMoviesBtn);
    topLayout->addWidget(m_filterBooksBtn);
    topLayout->addStretch();

    m_statsLabel = new QLabel();
    m_statsLabel->setStyleSheet("font-size: 12px; color: #A6968F;");
    topLayout->addWidget(m_statsLabel);

    rootLayout->addWidget(topBar);

    // Empty state widget
    m_emptyStateWidget = new QWidget();
    m_emptyStateWidget->setStyleSheet("background: transparent;");
    QVBoxLayout* emptyLayout = new QVBoxLayout(m_emptyStateWidget);
    emptyLayout->setContentsMargins(0, 60, 0, 0);
    emptyLayout->setAlignment(Qt::AlignCenter);

    QLabel* emptyIcon = new QLabel("♡");
    emptyIcon->setAlignment(Qt::AlignCenter);
    emptyIcon->setStyleSheet("font-size: 48px; color: #332A2C; margin-bottom: 12px;");
    QLabel* emptyTitle = new QLabel("В избранном пока пусто");
    emptyTitle->setAlignment(Qt::AlignCenter);
    emptyTitle->setStyleSheet("font-size: 18px; font-weight: bold; color: #F2E9E4;");
    QLabel* emptySub = new QLabel("Добавляйте понравившиеся фильмы и книги, нажимая на сердечко");
    emptySub->setAlignment(Qt::AlignCenter);
    emptySub->setStyleSheet("font-size: 13px; color: #A6968F; margin-top: 6px;");

    emptyLayout->addWidget(emptyIcon);
    emptyLayout->addWidget(emptyTitle);
    emptyLayout->addWidget(emptySub);
    rootLayout->addWidget(m_emptyStateWidget);

    // Scroll Area for cards
    m_scrollArea = new QScrollArea(this);
    m_scrollArea->setWidgetResizable(true);
    m_scrollArea->setStyleSheet("background: transparent; border: none;");

    m_cardsContainer = new QWidget();
    m_cardsContainer->setStyleSheet("background: transparent;");
    m_cardsLayout = new QGridLayout(m_cardsContainer);
    m_cardsLayout->setContentsMargins(0, 8, 0, 20);
    m_cardsLayout->setSpacing(16);
    m_cardsLayout->setAlignment(Qt::AlignLeft | Qt::AlignTop);

    m_scrollArea->setWidget(m_cardsContainer);
    rootLayout->addWidget(m_scrollArea, 1);

    updateFilterButtons();
    refresh();
}

void FavoritesPage::updateFilterButtons() {
    auto setBtn = [](QPushButton* btn, bool active) {
        btn->setProperty("active", active ? "true" : "false");
        btn->style()->unpolish(btn);
        btn->style()->polish(btn);
    };

    setBtn(m_filterAllBtn, m_currentFilter == "all");
    setBtn(m_filterMoviesBtn, m_currentFilter == "movies");
    setBtn(m_filterBooksBtn, m_currentFilter == "books");
}

void FavoritesPage::refresh() {
    QList<MediaItem> allFavs = m_data->getFavoriteItems();

    int movieCount = 0;
    int bookCount = 0;
    for (const auto& item : allFavs) {
        if (item.kind == MediaKind::Movie) movieCount++;
        else bookCount++;
    }
    m_statsLabel->setText(QString("Всего: %1   |   Фильмов: %2   |   Книг: %3")
                          .arg(allFavs.size()).arg(movieCount).arg(bookCount));

    m_filteredItems.clear();
    for (const auto& item : allFavs) {
        if (m_currentFilter == "all") {
            m_filteredItems.append(item);
        } else if (m_currentFilter == "movies" && item.kind == MediaKind::Movie) {
            m_filteredItems.append(item);
        } else if (m_currentFilter == "books" && item.kind == MediaKind::Book) {
            m_filteredItems.append(item);
        }
    }

    relayoutCards();
}

void FavoritesPage::resizeEvent(QResizeEvent* event) {
    QWidget::resizeEvent(event);
    relayoutCards();
}

void FavoritesPage::relayoutCards() {
    QLayoutItem* child;
    while ((child = m_cardsLayout->takeAt(0)) != nullptr) {
        if (child->widget()) {
            child->widget()->deleteLater();
        }
        delete child;
    }

    if (m_filteredItems.isEmpty()) {
        m_emptyStateWidget->setVisible(true);
        m_scrollArea->setVisible(false);
        return;
    }

    m_emptyStateWidget->setVisible(false);
    m_scrollArea->setVisible(true);

    int availableWidth = m_scrollArea->viewport()->width();
    if (availableWidth <= 0) availableWidth = 800;

    const int cardW = 188;
    const int gap = 16;
    int cols = std::max(1, availableWidth / (cardW + gap));

    for (int i = 0; i < m_filteredItems.size(); ++i) {
        int r = i / cols;
        int c = i % cols;
        CardWidget* card = new CardWidget(m_filteredItems[i], m_data, m_cardsContainer);
        connect(card, &CardWidget::openRequested, this, &FavoritesPage::openDetailsRequested);
        connect(card, &CardWidget::favoriteChanged, this, [this](const MediaItem& it) {
            emit favoriteChanged(it);
            refresh();
        });
        m_cardsLayout->addWidget(card, r, c);
    }
}

