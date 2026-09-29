#pragma once

#include <QWidget>
#include <QLabel>
#include <QComboBox>
#include <QPushButton>

class DataManager;
class MainWindow;

class SettingsPage : public QWidget {
    Q_OBJECT
public:
    explicit SettingsPage(DataManager* data, MainWindow* app, QWidget* parent = nullptr);

    void refresh();

signals:
    void favoritesCleared();

private:
    void setupUi();

    DataManager* m_data;
    MainWindow* m_app;

    QComboBox* m_themeCombo;
    QComboBox* m_scaleCombo;
    QLabel* m_favCountLabel;
};

