#include "list_page.h"
#include "card_widget.h"
#include "main_window.h"
#include "../services/data_manager.h"
#include "../styles/theme.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <algorithm>

ListPage::ListPage(DataManager* data, MainWindow* app,
                   std::function<QList<MediaItem>()> itemsGetter,
                   const QString& title, const QString& icon,
                   QWidget* parent)
    : QWidget(parent), m_data(data), m_app(app),
      m_itemsGetter(itemsGetter), m_title(title), m_icon(icon) {
    setupUi();
}

void ListPage::setupUi() {
    QVBoxLayout* rootLayout = new QVBoxLayout(this);
    rootLayout->setContentsMargins(36, 28, 36, 28);
    rootLayout->setSpacing(20);

    // Header (Title + Subtitle)
    QWidget* headerWidget = new QWidget();
    headerWidget->setStyleSheet("background: transparent;");
    QVBoxLayout* headerLayout = new QVBoxLayout(headerWidget);
    headerLayout->setContentsMargins(0, 0, 0, 0);
    headerLayout->setSpacing(4);

    m_titleLabel = new QLabel(m_title);
    m_titleLabel->setStyleSheet("font-size: 26px; font-weight: bold; color: #F2E9E4;");
    m_subtitleLabel = new QLabel(m_icon == "🎞" ? "Подборка фильмов по вашему вкусу" : "Истории, в которые стоит погрузиться");
    m_subtitleLabel->setStyleSheet("font-size: 13px; color: #A6968F;");
    headerLayout->addWidget(m_titleLabel);
    headerLayout->addWidget(m_subtitleLabel);
    rootLayout->addWidget(headerWidget);

    // Filters row (Search + Genre + Sort)
    QWidget* filterWidget = new QWidget();
    filterWidget->setStyleSheet("background: transparent;");
    QHBoxLayout* filterLayout = new QHBoxLayout(filterWidget);
    filterLayout->setContentsMargins(0, 0, 0, 0);
    filterLayout->setSpacing(12);

    m_searchBox = new QLineEdit();
    m_searchBox->setPlaceholderText("Быстрый поиск…");
    m_searchBox->setFixedWidth(240);
    connect(m_searchBox, &QLineEdit::textChanged, this, [this]() {
        if (!m_suppressEvents) refresh();
    });
    filterLayout->addWidget(m_searchBox);

    m_genreCombo = new QComboBox();
    m_genreCombo->setMinimumWidth(160);
    m_suppressEvents = true;
    m_genreCombo->addItem("Все");
    QStringList genres = m_data->genresFor(m_itemsGetter());
    m_genreCombo->addItems(genres);
    m_suppressEvents = false;
    connect(m_genreCombo, &QComboBox::currentIndexChanged, this, [this]() {
        if (!m_suppressEvents) refresh();
    });
    filterLayout->addWidget(m_genreCombo);

    m_sortCombo = new QComboBox();
    m_sortCombo->setMinimumWidth(150);
    m_sortCombo->addItems({ "По рейтингу", "По названию" });
    connect(m_sortCombo, &QComboBox::currentIndexChanged, this, [this]() {
        if (!m_suppressEvents) refresh();
    });
    filterLayout->addWidget(m_sortCombo);

    filterLayout->addStretch();
    rootLayout->addWidget(filterWidget);

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

    m_emptyLabel = new QLabel("Ничего не найдено");
    m_emptyLabel->setStyleSheet("font-size: 14px; color: #A6968F; margin-top: 30px;");
    m_emptyLabel->setVisible(false);
    m_cardsLayout->addWidget(m_emptyLabel, 0, 0);

    m_scrollArea->setWidget(m_cardsContainer);
    rootLayout->addWidget(m_scrollArea, 1);

    refresh();
}

void ListPage::setSearchQuery(const QString& query) {
    m_suppressEvents = true;
    m_searchBox->setText(query);
    m_genreCombo->setCurrentIndex(0);
    m_suppressEvents = false;
    refresh();
}

void ListPage::setGenreFilter(const QString& genre) {
    m_suppressEvents = true;
    m_searchBox->clear();
    int idx = m_genreCombo->findText(genre);
    m_genreCombo->setCurrentIndex(idx >= 0 ? idx : 0);
    m_suppressEvents = false;
    refresh();
}

void ListPage::refresh() {
    QList<MediaItem> items = m_itemsGetter();

    QString query = m_searchBox->text().trimmed();
    if (!query.isEmpty()) {
        items = m_data->search(query, items);
    }

    QString genre = m_genreCombo->currentText();
    if (!genre.isEmpty() && genre != "Все") {
        QList<MediaItem> filtered;
        for (const auto& it : items) {
            if (it.genre.contains(genre)) filtered.append(it);
        }
        items = filtered;
    }

    if (m_sortCombo->currentText() == "По названию") {
        std::sort(items.begin(), items.end(), [](const MediaItem& a, const MediaItem& b) {
            return a.title.localeAwareCompare(b.title) < 0;
        });
    } else {
        std::sort(items.begin(), items.end(), [](const MediaItem& a, const MediaItem& b) {
            return a.rating > b.rating;
        });
    }

    m_currentItems = items;
    relayoutCards();
}

void ListPage::resizeEvent(QResizeEvent* event) {
    QWidget::resizeEvent(event);
    relayoutCards();
}

void ListPage::relayoutCards() {
    // Clear layout
    QLayoutItem* child;
    while ((child = m_cardsLayout->takeAt(0)) != nullptr) {
        if (child->widget() && child->widget() != m_emptyLabel) {
            child->widget()->deleteLater();
        }
        delete child;
    }

    if (m_currentItems.isEmpty()) {
        m_emptyLabel->setVisible(true);
        m_cardsLayout->addWidget(m_emptyLabel, 0, 0);
        return;
    }
    m_emptyLabel->setVisible(false);

    int availableWidth = m_scrollArea->viewport()->width();
    if (availableWidth <= 0) availableWidth = 800;

    const int cardW = 188;
    const int gap = 16;
    int cols = std::max(1, availableWidth / (cardW + gap));

    for (int i = 0; i < m_currentItems.size(); ++i) {
        int r = i / cols;
        int c = i % cols;
        CardWidget* card = new CardWidget(m_currentItems[i], m_data, m_cardsContainer);
        connect(card, &CardWidget::openRequested, this, &ListPage::openDetailsRequested);
        connect(card, &CardWidget::favoriteChanged, this, &ListPage::favoriteChanged);
        m_cardsLayout->addWidget(card, r, c);
    }
}

