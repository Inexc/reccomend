#include "sidebar.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QLabel>
#include <QStyle>

Sidebar::Sidebar(QWidget* parent)
    : QWidget(parent) {
    setFixedWidth(190);
    setStyleSheet("background-color: #141013; border-right: 1px solid #332A2C;");
    setupUi();
}

void Sidebar::setupUi() {
    QVBoxLayout* layout = new QVBoxLayout(this);
    layout->setContentsMargins(14, 24, 14, 24);
    layout->setSpacing(6);

    // Header logo
    QWidget* logoWidget = new QWidget();
    logoWidget->setStyleSheet("border: none;");
    QHBoxLayout* logoLayout = new QHBoxLayout(logoWidget);
    logoLayout->setContentsMargins(4, 0, 4, 16);
    logoLayout->setSpacing(10);

    QLabel* iconBadge = new QLabel("🎬");
    iconBadge->setFixedSize(32, 32);
    iconBadge->setAlignment(Qt::AlignCenter);
    iconBadge->setStyleSheet(
        "background: qlineargradient(x1:0, y1:0, x2:1, y2:1, stop:0 #C1666B, stop:1 #A6535C); "
        "border-radius: 8px; font-size: 14px;"
    );

    QLabel* titleLabel = new QLabel("Рекомендации");
    titleLabel->setStyleSheet("font-size: 13px; font-weight: bold; color: #F2E9E4; border: none;");

    logoLayout->addWidget(iconBadge);
    logoLayout->addWidget(titleLabel);
    logoLayout->addStretch();
    layout->addWidget(logoWidget);

    // Navigation items
    struct NavItem {
        QString key;
        QString text;
    };

    const QList<NavItem> items = {
        { "home", "🎬  Главная" },
        { "movies", "🎞  Фильмы" },
        { "books", "📖  Книги" },
        { "favorites", "♡  Избранное" },
        { "settings", "⚙  Настройки" }
    };

    for (const auto& item : items) {
        QPushButton* btn = new QPushButton(item.text);
        btn->setObjectName("sidebarBtn");
        btn->setCursor(Qt::PointingHandCursor);
        btn->setFixedHeight(40);
        connect(btn, &QPushButton::clicked, this, [this, key = item.key]() {
            setActive(key);
            emit navigate(key);
        });
        m_navButtons[item.key] = btn;
        layout->addWidget(btn);
    }

    layout->addStretch();

    // Footer text
    QLabel* footer1 = new QLabel("Хорошие истории");
    footer1->setStyleSheet("font-size: 11px; color: #6E5F5A; border: none; padding-left: 8px;");
    QLabel* footer2 = new QLabel("всегда рядом");
    footer2->setStyleSheet("font-size: 11px; color: #6E5F5A; border: none; padding-left: 8px;");
    layout->addWidget(footer1);
    layout->addWidget(footer2);

    setActive("home");
}

void Sidebar::setActive(const QString& key) {
    m_activeKey = key;
    for (auto it = m_navButtons.begin(); it != m_navButtons.end(); ++it) {
        bool isActive = (it.key() == key);
        it.value()->setProperty("active", isActive ? "true" : "false");
        it.value()->style()->unpolish(it.value());
        it.value()->style()->polish(it.value());
    }
}

