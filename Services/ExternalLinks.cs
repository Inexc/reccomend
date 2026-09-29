using System;
using System.Diagnostics;
using MovieBookRecommender.Models;

namespace MovieBookRecommender.Services
{
    /// <summary>
    /// Строит ссылки на легальные внешние сервисы для кнопок «Смотреть»/«Читать»
    /// и открывает их в браузере по умолчанию.
    ///
    /// Фильмы → Кинопоиск (страница поиска по названию — у Кинопоиска нет
    /// открытого API для точного ID без ключа, поэтому используется поиск).
    /// Книги → ЛитРес (поиск по названию и автору — легальная покупка/чтение).
    /// </summary>
    public static class ExternalLinks
    {
        public static string BuildUrl(MediaItem item)
        {
            return item.Kind == MediaKind.Movie
                ? $"https://www.kinopoisk.ru/index.php?kp_query={Uri.EscapeDataString(item.Title)}"
                : $"https://www.litres.ru/pages/biblio_search/?q={Uri.EscapeDataString(item.Title + " " + item.Author)}";
        }

        public static void Open(MediaItem item)
        {
            var url = BuildUrl(item);
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                // если браузер не удалось открыть (нет ассоциации и т.п.) — не роняем приложение
            }
        }
    }
}
