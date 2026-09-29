cmake_minimum_required(VERSION 3.16)
project(MovieBookRecommenderQt LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

set(CMAKE_AUTOMOC ON)
set(CMAKE_AUTORCC ON)
set(CMAKE_AUTOUIC ON)

find_package(Qt6 REQUIRED COMPONENTS Widgets Network)

set(SOURCES
    src/main.cpp
    src/config.h
    src/models/media_item.h
    src/services/data_manager.h
    src/services/data_manager.cpp
    src/services/recommendation_service.h
    src/services/recommendation_service.cpp
    src/services/poster_service.h
    src/services/poster_service.cpp
    src/services/external_links.h
    src/services/external_links.cpp
    src/styles/theme.h
    src/styles/theme.cpp
    src/views/card_widget.h
    src/views/card_widget.cpp
    src/views/mini_card_widget.h
    src/views/mini_card_widget.cpp
    src/views/details_panel.h
    src/views/details_panel.cpp
    src/views/sidebar.h
    src/views/sidebar.cpp
    src/views/home_page.h
    src/views/home_page.cpp
    src/views/list_page.h
    src/views/list_page.cpp
    src/views/favorites_page.h
    src/views/favorites_page.cpp
    src/views/settings_page.h
    src/views/settings_page.cpp
    src/views/main_window.h
    src/views/main_window.cpp
)

add_executable(MovieBookRecommender ${SOURCES})

target_link_libraries(MovieBookRecommender PRIVATE
    Qt6::Widgets
    Qt6::Network
)

# Copy Data and Assets directories to the build directory
add_custom_command(TARGET MovieBookRecommender POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E copy_directory
        "${CMAKE_CURRENT_SOURCE_DIR}/Data"
        "$<TARGET_FILE_DIR:MovieBookRecommender>/Data"
    COMMAND ${CMAKE_COMMAND} -E copy_directory
        "${CMAKE_CURRENT_SOURCE_DIR}/Assets"
        "$<TARGET_FILE_DIR:MovieBookRecommender>/Assets"
    COMMENT "Copying Data and Assets folders to build target directory"
)

