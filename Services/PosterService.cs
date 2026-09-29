using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MovieBookRecommender.Models;
using AppConfig = MovieBookRecommender.Config;

namespace MovieBookRecommender.Services
{
    /// <summary>
    /// Загружает настоящие постеры фильмов (TMDb) и обложки книг (Open Library),
    /// либо — если ключа/сети/результата нет — рисует цветную заглушку с первой
    /// буквой и названием (аналог utils/data_manager.py::_generate_placeholder
    /// из Python-версии, только средствами WPF вместо Pillow).
    ///
    /// Любая сетевая ошибка перехватывается: программа никогда не падает
    /// из-за отсутствия интернета или ключа — просто используется заглушка.
    /// </summary>
    public static class PosterService
    {
        private const int PosterW = 300;
        private const int PosterH = 440;

        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        static PosterService()
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("movie-book-recommender/1.0");
        }

        /// <summary>Гарантирует, что у элемента есть файл постера на диске
        /// (скачивает при возможности, иначе генерирует заглушку).</summary>
        public static async Task EnsurePosterAsync(MediaItem item)
        {
            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, item.Poster);
            if (File.Exists(fullPath))
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            var fetched = false;
            if (AppConfig.UseOnlinePosters)
            {
                try
                {
                    fetched = item.Kind == MediaKind.Movie
                        ? await FetchMoviePosterAsync(item, fullPath)
                        : await FetchBookCoverAsync(item, fullPath);
                }
                catch (Exception ex)
                {
                    fetched = false; // сеть недоступна и т.п. — не критично
                    LogError(item, ex);
                }
            }

            if (!fetched)
                GeneratePlaceholder(item, fullPath);
        }

        /// <summary>Временный диагностический лог — пишет причину неудачи
        /// в poster_errors.log рядом с exe, чтобы можно было понять,
        /// почему не скачался постер (сеть, ключ, антивирус и т.п.).</summary>
        private static void LogError(MediaItem item, Exception ex)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "poster_errors.log");
                var line = $"[{DateTime.Now:HH:mm:ss}] {item.Title} ({item.Kind}): {ex.GetType().Name}: {ex.Message}{Environment.NewLine}";
                File.AppendAllText(logPath, line);
            }
            catch
            {
                // логирование не должно ронять приложение
            }
        }

        private static void LogInfo(MediaItem item, string message)
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "poster_errors.log");
                var line = $"[{DateTime.Now:HH:mm:ss}] {item.Title} ({item.Kind}): {message}{Environment.NewLine}";
                File.AppendAllText(logPath, line);
            }
            catch
            {
                // логирование не должно ронять приложение
            }
        }

        // ------------------------------------------------------------------
        // TMDb — постеры фильмов
        // ------------------------------------------------------------------
        private static async Task<bool> FetchMoviePosterAsync(MediaItem item, string destPath)
        {
            if (string.IsNullOrWhiteSpace(AppConfig.TmdbApiKey))
            {
                LogInfo(item, "пропущено: TmdbApiKey пуст");
                return false;
            }

            var query = Uri.EscapeDataString(item.Title);
            var url = $"https://api.themoviedb.org/3/search/movie?api_key={AppConfig.TmdbApiKey}" +
                      $"&query={query}&language=ru-RU&year={item.Year}";

            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
            {
                // пробуем без года
                var url2 = $"https://api.themoviedb.org/3/search/movie?api_key={AppConfig.TmdbApiKey}" +
                           $"&query={query}&language=ru-RU";
                json = await Http.GetStringAsync(url2);
                using var doc2 = JsonDocument.Parse(json);
                if (!doc2.RootElement.TryGetProperty("results", out results) || results.GetArrayLength() == 0)
                {
                    LogInfo(item, "TMDb: результатов поиска не найдено");
                    return false;
                }
            }

            var first = results[0];
            if (!first.TryGetProperty("poster_path", out var posterPathEl) || posterPathEl.ValueKind == JsonValueKind.Null)
            {
                LogInfo(item, "TMDb: найден фильм, но у него нет poster_path");
                return false;
            }

            var posterUrl = "https://image.tmdb.org/t/p/w500" + posterPathEl.GetString();
            var bytes = await Http.GetByteArrayAsync(posterUrl);
            await File.WriteAllBytesAsync(destPath, bytes);
            LogInfo(item, "OK: постер скачан с TMDb");
            return true;
        }

        // ------------------------------------------------------------------
        // Open Library — обложки книг (ключ не требуется)
        // ------------------------------------------------------------------
        private static async Task<bool> FetchBookCoverAsync(MediaItem item, string destPath)
        {
            var title = Uri.EscapeDataString(item.Title);
            var url = $"https://openlibrary.org/search.json?title={title}&limit=5";

            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("docs", out var docs) || docs.GetArrayLength() == 0)
            {
                LogInfo(item, "OpenLibrary: книга не найдена");
                return false;
            }

            long? coverId = null;
            foreach (var d in docs.EnumerateArray())
            {
                if (d.TryGetProperty("cover_i", out var c) && c.ValueKind == JsonValueKind.Number)
                {
                    coverId = c.GetInt64();
                    break;
                }
            }
            if (coverId is null)
            {
                LogInfo(item, "OpenLibrary: у найденных изданий нет обложки (cover_i)");
                return false;
            }

            var coverUrl = $"https://covers.openlibrary.org/b/id/{coverId}-L.jpg";
            var bytes = await Http.GetByteArrayAsync(coverUrl);
            await File.WriteAllBytesAsync(destPath, bytes);
            LogInfo(item, "OK: обложка скачана с Open Library");
            return true;
        }

        // ------------------------------------------------------------------
        // Заглушка: спокойный тональный градиент в фирменной палитре + буква
        // ------------------------------------------------------------------
        private static void GeneratePlaceholder(MediaItem item, string destPath)
        {
            var (top, bottom) = ColorFor(item.Title);

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var rect = new Rect(0, 0, PosterW, PosterH);
                var brush = new LinearGradientBrush(top, bottom, 90);
                dc.DrawRectangle(brush, null, rect);

                // Одна крупная, но приглушённая буква по центру — без дублирования
                // названия (оно и так отображается поверх карточки в самом приложении).
                var letter = string.IsNullOrWhiteSpace(item.Title) ? "?" : item.Title.Trim()[0].ToString().ToUpper();
                var letterBrush = new SolidColorBrush(Color.FromArgb(70, 242, 233, 228)); // TextBrush при низкой непрозрачности
                var letterText = new FormattedText(
                    letter, System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI Semibold"), 68, letterBrush,
                    VisualTreeHelper.GetDpi(visual).PixelsPerDip);
                dc.DrawText(letterText, new Point((PosterW - letterText.Width) / 2, (PosterH - letterText.Height) / 2));
            }

            var bmp = new RenderTargetBitmap(PosterW, PosterH, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            using var stream = File.Create(destPath);
            encoder.Save(stream);
        }

        private static (Color top, Color bottom) ColorFor(string text)
        {
            // Всегда в рамках фирменной палитры (карточка #221A1E) — лишь лёгкий
            // тональный разброс между произведениями, без ярких/разноцветных оттенков.
            var seed = Math.Abs(text.GetHashCode()) % 3;
            return seed switch
            {
                0 => (Color.FromRgb(0x2E, 0x23, 0x27), Color.FromRgb(0x22, 0x1A, 0x1E)),
                1 => (Color.FromRgb(0x2B, 0x21, 0x29), Color.FromRgb(0x22, 0x1A, 0x1E)),
                _ => (Color.FromRgb(0x29, 0x21, 0x24), Color.FromRgb(0x22, 0x1A, 0x1E)),
            };
        }
    }
}
