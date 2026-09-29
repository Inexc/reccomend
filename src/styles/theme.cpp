#include "theme.h"

QString Theme::getStyleSheet() {
    return R"(
        QWidget {
            background-color: #161114;
            color: #F2E9E4;
            font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif;
            font-size: 13px;
        }

        QMainWindow {
            background-color: #161114;
        }

        QScrollArea {
            background: transparent;
            border: none;
        }

        QScrollBar:vertical {
            border: none;
            background: #141013;
            width: 8px;
            margin: 0px 0px 0px 0px;
            border-radius: 4px;
        }

        QScrollBar::handle:vertical {
            background: #332A2C;
            min-height: 25px;
            border-radius: 4px;
        }

        QScrollBar::handle:vertical:hover {
            background: #C1666B;
        }

        QScrollBar::add-line:vertical, QScrollBar::sub-line:vertical {
            border: none;
            background: none;
            height: 0px;
        }

        QScrollBar:horizontal {
            border: none;
            background: #141013;
            height: 8px;
            margin: 0px 0px 0px 0px;
            border-radius: 4px;
        }

        QScrollBar::handle:horizontal {
            background: #332A2C;
            min-width: 25px;
            border-radius: 4px;
        }

        QScrollBar::handle:horizontal:hover {
            background: #C1666B;
        }

        QScrollBar::add-line:horizontal, QScrollBar::sub-line:horizontal {
            border: none;
            background: none;
            width: 0px;
        }

        QLineEdit {
            background-color: #221A1E;
            border: 1px solid #332A2C;
            border-radius: 10px;
            padding: 8px 14px;
            color: #F2E9E4;
            font-size: 13px;
            selection-background-color: #C1666B;
        }

        QLineEdit:focus {
            border: 1px solid #C1666B;
        }

        QLineEdit::placeholder {
            color: #6E5F5A;
        }

        QComboBox {
            background-color: #221A1E;
            border: 1px solid #332A2C;
            border-radius: 8px;
            padding: 6px 12px;
            color: #F2E9E4;
            font-size: 13px;
            min-height: 20px;
        }

        QComboBox:hover {
            border: 1px solid #C1666B;
        }

        QComboBox::drop-down {
            subcontrol-origin: padding;
            subcontrol-position: top right;
            width: 20px;
            border-left-width: 0px;
            border-top-right-radius: 8px;
            border-bottom-right-radius: 8px;
        }

        QComboBox QAbstractItemView {
            background-color: #221A1E;
            border: 1px solid #332A2C;
            border-radius: 8px;
            color: #F2E9E4;
            selection-background-color: #C1666B;
            selection-color: #FFFFFF;
            padding: 4px;
            outline: none;
        }

        QPushButton {
            background-color: #221A1E;
            border: 1px solid #332A2C;
            border-radius: 8px;
            color: #F2E9E4;
            padding: 8px 16px;
            font-weight: 500;
        }

        QPushButton:hover {
            background-color: #2A2024;
            border: 1px solid #C1666B;
        }

        QPushButton:pressed {
            background-color: #1C1619;
        }

        QPushButton#accentBtn {
            background-color: #C1666B;
            border: none;
            color: #FFFFFF;
            font-weight: 600;
            border-radius: 8px;
            padding: 10px 22px;
        }

        QPushButton#accentBtn:hover {
            background-color: #CD7A7F;
        }

        QPushButton#accentBtn:pressed {
            background-color: #A8555A;
        }

        QPushButton#ghostBtn {
            background-color: transparent;
            border: 1px solid #332A2C;
            color: #F2E9E4;
            font-weight: 500;
            border-radius: 8px;
            padding: 10px 18px;
        }

        QPushButton#ghostBtn:hover {
            background-color: #221A1E;
            border: 1px solid #C1666B;
        }

        QPushButton#roundIconBtn {
            background-color: #221A1E;
            border: 1px solid #332A2C;
            border-radius: 16px;
            color: #F2E9E4;
            min-width: 32px;
            max-width: 32px;
            min-height: 32px;
            max-height: 32px;
            font-weight: bold;
            font-size: 14px;
            padding: 0px;
        }

        QPushButton#roundIconBtn:hover {
            background-color: #C1666B;
            color: #FFFFFF;
            border: 1px solid #C1666B;
        }

        QPushButton#chipBtn {
            background-color: #221A1E;
            border: 1px solid #332A2C;
            border-radius: 12px;
            color: #A6968F;
            font-size: 11px;
            padding: 5px 12px;
        }

        QPushButton#chipBtn:hover {
            border: 1px solid #C1666B;
            color: #F2E9E4;
        }

        QPushButton#chipBtn[active="true"] {
            background-color: #C1666B;
            border: 1px solid #C1666B;
            color: #FFFFFF;
            font-weight: 600;
        }

        QPushButton#sidebarBtn {
            background-color: transparent;
            border: none;
            border-radius: 8px;
            color: #A6968F;
            font-size: 13px;
            font-weight: 500;
            text-align: left;
            padding: 10px 14px;
        }

        QPushButton#sidebarBtn:hover {
            background-color: #221A1E;
            color: #F2E9E4;
        }

        QPushButton#sidebarBtn[active="true"] {
            background: qlineargradient(x1:0, y1:0, x2:1, y2:1, stop:0 #C1666B, stop:1 #A6535C);
            color: #FFFFFF;
            font-weight: 600;
        }

        QMessageBox {
            background-color: #1C1619;
        }

        QMessageBox QLabel {
            color: #F2E9E4;
            font-size: 13px;
        }

        QMessageBox QPushButton {
            min-width: 70px;
        }
    )";
}

