#include "main_window.h"
#include "sidebar.h"
#include "details_panel.h"
#include "home_page.h"
#include "list_page.h"
#include "favorites_page.h"
#include "settings_page.h"
#include "../services/recommendation_service.h"
#include "../services/poster_service.h"
#include <QHBoxLayout>

MainWindow::MainWindow(QWidget* parent)
    : QMainWindow(parent) {
    setWindowTitle("Рекомендации фильмов и книг");
    resize(1400, 850);
    setMinimumSize(1100, 700);

    m_data.load();

    // Ensure initial placeholders exist so everything renders instantly
    for (const auto& item : m_data.allItems()) {
        PosterService::instance().ensurePoster(item);
    }

    setupUi();
    showFeaturedDefault();
    showPage("home");
}

void MainWindow::setupUi() {
    QWidget* central = new QWidget(this);
    setCentralWidget(central);

    QHBoxLayout* rootLayout = new QHBoxLayout(central);
    rootLayout->setContentsMargins(0, 0, 0, 0);
    rootLayout->setSpacing(0);

    // Sidebar
    m_sidebar = new Sidebar(this);
    connect(m_sidebar, &Sidebar::navigate, this, [this](const QString& key) {
        showPage(key);
    });
    rootLayout->addWidget(m_sidebar);

    // Center Pages Stack
    m_pageStack = new QStackedWidget(this);
    m_pageStack->setStyleSheet("background-color: #161114;");

    m_homePage = new HomePage(&m_data, this, this);
    connect(m_homePage, &HomePage::openDetailsRequested, this, &MainWindow::openItem);
    connect(m_homePage, &HomePage::searchRequested, this, [this](const QString& q) {
        showPage("movies", q);
    });
    connect(m_homePage, &HomePage::genreSelected, this, [this](const QString& g) {
        showPage("movies", "", g);
    });

    m_moviesPage = new ListPage(&m_data, this, [this]() { return m_data.getMovies(); }, "Фильмы", "🎞", this);
    connect(m_moviesPage, &ListPage::openDetailsRequested, this, &MainWindow::openItem);
    connect(m_moviesPage, &ListPage::favoriteChanged, this, &MainWindow::onFavoriteChanged);

    m_booksPage = new ListPage(&m_data, this, [this]() { return m_data.getBooks(); }, "Книги", "📖", this);
    connect(m_booksPage, &ListPage::openDetailsRequested, this, &MainWindow::openItem);
    connect(m_booksPage, &ListPage::favoriteChanged, this, &MainWindow::onFavoriteChanged);

    m_favoritesPage = new FavoritesPage(&m_data, this, this);
    connect(m_favoritesPage, &FavoritesPage::openDetailsRequested, this, &MainWindow::openItem);
    connect(m_favoritesPage, &FavoritesPage::favoriteChanged, this, &MainWindow::onFavoriteChanged);

    m_settingsPage = new SettingsPage(&m_data, this, this);
    connect(m_settingsPage, &SettingsPage::favoritesCleared, this, [this]() {
        m_homePage->refresh();
        m_favoritesPage->refresh();
        m_moviesPage->refresh();
        m_booksPage->refresh();
        showFeaturedDefault();
    });

    m_pageIndices["home"] = m_pageStack->addWidget(m_homePage);
    m_pageIndices["movies"] = m_pageStack->addWidget(m_moviesPage);
    m_pageIndices["books"] = m_pageStack->addWidget(m_booksPage);
    m_pageIndices["favorites"] = m_pageStack->addWidget(m_favoritesPage);
    m_pageIndices["settings"] = m_pageStack->addWidget(m_settingsPage);

    rootLayout->addWidget(m_pageStack, 1);

    // Details Panel
    m_detailsPanel = new DetailsPanel(&m_data, this);
    connect(m_detailsPanel, &DetailsPanel::openRequested, this, &MainWindow::openItem);
    connect(m_detailsPanel, &DetailsPanel::favoriteChanged, this, &MainWindow::onFavoriteChanged);
    rootLayout->addWidget(m_detailsPanel);
}

void MainWindow::showFeaturedDefault() {
    const MediaItem* featured = RecommendationService::pickFeatured(m_data.allItems(), m_data.getFavorites());
    m_detailsPanel->showItem(featured);
}

void MainWindow::openItem(const MediaItem& item) {
    m_detailsPanel->showItem(&item);
}

void MainWindow::onFavoriteChanged(const MediaItem&) {
    if (m_currentPageKey == "favorites") {
        m_favoritesPage->refresh();
    }
    m_detailsPanel->updateFavoriteState();
    m_settingsPage->refresh();
}

void MainWindow::showPage(const QString& key, const QString& query, const QString& genreFilter) {
    if (!m_pageIndices.contains(key)) return;

    m_currentPageKey = key;
    m_pageStack->setCurrentIndex(m_pageIndices[key]);
    m_sidebar->setActive(key);

    if (key == "movies") {
        if (!genreFilter.isEmpty()) {
            m_moviesPage->setGenreFilter(genreFilter);
        } else if (!query.isEmpty()) {
            m_moviesPage->setSearchQuery(query);
        } else {
            m_moviesPage->refresh();
        }
    } else if (key == "books") {
        if (!genreFilter.isEmpty()) {
            m_booksPage->setGenreFilter(genreFilter);
        } else if (!query.isEmpty()) {
            m_booksPage->setSearchQuery(query);
        } else {
            m_booksPage->refresh();
        }
    } else if (key == "home") {
        m_homePage->refresh();
    } else if (key == "favorites") {
        m_favoritesPage->refresh();
    } else if (key == "settings") {
        m_settingsPage->refresh();
    }
}

