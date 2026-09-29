#include "settings_page.h"
#include "main_window.h"
#include "../services/data_manager.h"
#include <QVBoxLayout>
#include <QHBoxLayout>
#include <QMessageBox>

SettingsPage::SettingsPage(DataManager* data, MainWindow* app, QWidget* parent)
    : QWidget(parent), m_data(data), m_app(app) {
    setupUi();
}

void SettingsPage::setupUi() {
    QVBoxLayout* rootLayout = new QVBoxLayout(this);
    rootLayout->setContentsMargins(36, 28, 36, 28);
    rootLayout->setSpacing(24);

    QLabel* titleLabel = new QLabel("Настройки");
    titleLabel->setStyleSheet("font-size: 26px; font-weight: bold; color: #F2E9E4;");
    rootLayout->addWidget(titleLabel);

    // Section 1: Appearance
    QWidget* section1 = new QWidget();
    section1->setStyleSheet("background-color: #221A1E; border: 1px solid #332A2C; border-radius: 12px;");
    QVBoxLayout* s1Layout = new QVBoxLayout(section1);
    s1Layout->setContentsMargins(20, 20, 20, 20);
    s1Layout->setSpacing(16);

    QLabel* s1Title = new QLabel("ВНЕШНИЙ ВИД");
    s1Title->setStyleSheet("font-size: 11px; font-weight: bold; color: #6E5F5A; letter-spacing: 1px; border: none;");
    s1Layout->addWidget(s1Title);

    // Theme row
    QWidget* themeRow = new QWidget();
    themeRow->setStyleSheet("border: none;");
    QHBoxLayout* themeLayout = new QHBoxLayout(themeRow);
    themeLayout->setContentsMargins(0, 0, 0, 0);
    QLabel* themeLabel = new QLabel("Тема оформления");
    themeLabel->setStyleSheet("font-size: 13px; color: #F2E9E4; border: none;");
    m_themeCombo = new QComboBox();
    m_themeCombo->addItems({ "Тёмная («Тёплый нуар»)", "Светлая" });
    m_themeCombo->setFixedWidth(220);
    connect(m_themeCombo, &QComboBox::currentIndexChanged, this, [this](int idx) {
        if (idx == 1) {
            QMessageBox::information(this, "Тема",
                "В этой версии приложение оптимизировано под тёмную тему «Тёплый нуар».\n"
                "Светлая тема будет добавлена в следующем обновлении.");
            m_themeCombo->setCurrentIndex(0);
        }
    });
    themeLayout->addWidget(themeLabel);
    themeLayout->addStretch();
    themeLayout->addWidget(m_themeCombo);
    s1Layout->addWidget(themeRow);

    // Scale row
    QWidget* scaleRow = new QWidget();
    scaleRow->setStyleSheet("border: none;");
    QHBoxLayout* scaleLayout = new QHBoxLayout(scaleRow);
    scaleLayout->setContentsMargins(0, 0, 0, 0);
    QLabel* scaleLabel = new QLabel("Масштаб интерфейса");
    scaleLabel->setStyleSheet("font-size: 13px; color: #F2E9E4; border: none;");
    m_scaleCombo = new QComboBox();
    m_scaleCombo->addItems({ "Компактный", "Обычный (100%)", "Крупный" });
    m_scaleCombo->setCurrentIndex(1);
    m_scaleCombo->setFixedWidth(220);
    scaleLayout->addWidget(scaleLabel);
    scaleLayout->addStretch();
    scaleLayout->addWidget(m_scaleCombo);
    s1Layout->addWidget(scaleRow);

    rootLayout->addWidget(section1);

    // Section 2: Data & Favorites
    QWidget* section2 = new QWidget();
    section2->setStyleSheet("background-color: #221A1E; border: 1px solid #332A2C; border-radius: 12px;");
    QVBoxLayout* s2Layout = new QVBoxLayout(section2);
    s2Layout->setContentsMargins(20, 20, 20, 20);
    s2Layout->setSpacing(16);

    QLabel* s2Title = new QLabel("УПРАВЛЕНИЕ ДАННЫМИ");
    s2Title->setStyleSheet("font-size: 11px; font-weight: bold; color: #6E5F5A; letter-spacing: 1px; border: none;");
    s2Layout->addWidget(s2Title);

    // Favorites info row
    QWidget* favInfoRow = new QWidget();
    favInfoRow->setStyleSheet("border: none;");
    QHBoxLayout* favInfoLayout = new QHBoxLayout(favInfoRow);
    favInfoLayout->setContentsMargins(0, 0, 0, 0);
    QLabel* favCountTitle = new QLabel("Элементов в избранном:");
    favCountTitle->setStyleSheet("font-size: 13px; color: #F2E9E4; border: none;");
    m_favCountLabel = new QLabel("0");
    m_favCountLabel->setStyleSheet("font-size: 13px; font-weight: bold; color: #D4B483; border: none;");
    favInfoLayout->addWidget(favCountTitle);
    favInfoLayout->addWidget(m_favCountLabel);
    favInfoLayout->addStretch();

    QPushButton* clearFavsBtn = new QPushButton("Очистить избранное");
    clearFavsBtn->setObjectName("ghostBtn");
    clearFavsBtn->setCursor(Qt::PointingHandCursor);
    clearFavsBtn->setStyleSheet("border-color: #B25360; color: #B25360;");
    connect(clearFavsBtn, &QPushButton::clicked, this, [this]() {
        auto res = QMessageBox::question(this, "Подтверждение",
            "Вы уверены, что хотите очистить весь список избранного?",
            QMessageBox::Yes | QMessageBox::No);
        if (res == QMessageBox::Yes) {
            m_data->clearFavorites();
            refresh();
            emit favoritesCleared();
            QMessageBox::information(this, "Готово", "Список избранного очищен.");
        }
    });
    favInfoLayout->addWidget(clearFavsBtn);
    s2Layout->addWidget(favInfoRow);

    rootLayout->addWidget(section2);

    // Section 3: About
    QWidget* section3 = new QWidget();
    section3->setStyleSheet("background-color: #221A1E; border: 1px solid #332A2C; border-radius: 12px;");
    QVBoxLayout* s3Layout = new QVBoxLayout(section3);
    s3Layout->setContentsMargins(20, 20, 20, 20);
    s3Layout->setSpacing(8);

    QLabel* s3Title = new QLabel("О ПРОГРАММЕ");
    s3Title->setStyleSheet("font-size: 11px; font-weight: bold; color: #6E5F5A; letter-spacing: 1px; border: none;");
    s3Layout->addWidget(s3Title);

    QLabel* appDesc = new QLabel("Интеллектуальная система рекомендаций фильмов и книг.\n"
                                 "Версия: 2.0 (Qt 6 C++)\n"
                                 "Разработано в рамках курса ТРПО.");
    appDesc->setStyleSheet("font-size: 12px; color: #A6968F; line-height: 18px; border: none;");
    s3Layout->addWidget(appDesc);

    rootLayout->addWidget(section3);
    rootLayout->addStretch();

    refresh();
}

void SettingsPage::refresh() {
    m_favCountLabel->setText(QString::number(m_data->getFavorites().size()));
}

