#pragma once

#include <QWidget>
#include <QPushButton>
#include <QMap>

class Sidebar : public QWidget {
    Q_OBJECT
public:
    explicit Sidebar(QWidget* parent = nullptr);

    void setActive(const QString& key);

signals:
    void navigate(const QString& key);

private:
    void setupUi();

    QMap<QString, QPushButton*> m_navButtons;
    QString m_activeKey;
};

