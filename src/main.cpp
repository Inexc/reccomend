#include <QApplication>
#include "views/main_window.h"
#include "styles/theme.h"

int main(int argc, char* argv[]) {
    QApplication app(argc, argv);
    app.setApplicationName("MovieBookRecommenderQt");
    app.setOrganizationName("TRPO");

    app.setStyleSheet(Theme::getStyleSheet());

    MainWindow window;
    window.show();

    return app.exec();
}

