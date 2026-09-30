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
    /// либо — если ключа/сети/результата нет — генерирует стильную обложку-заглушку.
    /// Файлы сохраняются в LocalAppData, скачивание происходит во временный файл
    /// с проверкой декодированием перед сохранением.
    /// </summary>
    public static class PosterService
    {
        private const int PosterW = 300;
        private const int PosterH = 440;

        public static readonly string UserDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MovieBookRecommender");

        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(6)
        };

        static PosterService()
        {
            Http.DefaultRequestHeaders.UserAgent.ParseAdd("movie-book-recommender/1.0");
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource> ImageCache = new();

        /// <summary>
        /// Разрешает существующий файл постера: сначала в UserData, затем в BaseDirectory.
        /// Проверяет как исходное расширение, так и альтернативное (.png/.jpg).
        /// </summary>
        public static string? ResolveExistingPosterPath(MediaItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Poster)) return null;

            string rel = item.Poster;
            string ext = Path.GetExtension(rel);
            string altExt = ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? Path.ChangeExtension(rel, ".jpg")
                : Path.ChangeExtension(rel, ".png");

            string[] candidates = {
                Path.Combine(UserDataDir, rel),
                Path.Combine(UserDataDir, altExt),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, rel),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, altExt),
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c))
                    return c;
            }
            return null;
        }

        /// <summary>
        /// Возвращает кэшированное изображение постера. Декодируется из файла ровно один раз
        /// и замораживается (Freeze) для максимальной производительности интерфейса.
        /// </summary>
        public static ImageSource? GetImage(MediaItem? item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Poster))
                return null;

            if (ImageCache.TryGetValue(item.Poster, out var cached))
                return cached;

            var fullPath = ResolveExistingPosterPath(item);
            if (fullPath == null)
            {
                try
                {
                    var defaultPath = Path.Combine(UserDataDir, item.Poster);
                    var dir = Path.GetDirectoryName(defaultPath);
                    if (!string.IsNullOrEmpty(dir))
                        Directory.CreateDirectory(dir);
                    GeneratePlaceholder(item, defaultPath);
                    fullPath = defaultPath;
                }
                catch
                {
                    // безопасно игнорируем ошибку генерации
                }
            }

            if (fullPath != null && File.Exists(fullPath))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 300;
                    bmp.UriSource = new Uri(fullPath, UriKind.Absolute);
                    bmp.EndInit();
                    bmp.Freeze();
                    ImageCache[item.Poster] = bmp;
                    return bmp;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        /// <summary>Очистка кэша изображений при необходимости.</summary>
        public static void ClearCache() => ImageCache.Clear();

        /// <summary>
        /// Гарантирует, что у элемента есть файл постера на диске
        /// (скачивает при возможности, иначе генерирует заглушку).
        /// Безопасно завёрнут в try/catch.
        /// </summary>
        public static async Task EnsurePosterAsync(MediaItem item)
        {
            try
            {
                var existing = ResolveExistingPosterPath(item);
                if (existing != null)
                    return;

                var targetPath = Path.Combine(UserDataDir, item.Poster);
                var dir = Path.GetDirectoryName(targetPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                var fetched = false;
                if (AppConfig.UseOnlinePosters)
                {
                    try
                    {
                        fetched = item.Kind == MediaKind.Movie
                            ? await FetchMoviePosterAsync(item, targetPath)
                            : await FetchBookCoverAsync(item, targetPath);
                    }
                    catch (Exception ex)
                    {
                        fetched = false;
                        LogError(item, ex);
                    }
                }

                if (!fetched)
                {
                    try
                    {
                        GeneratePlaceholder(item, targetPath);
                    }
                    catch (Exception ex)
                    {
                        LogError(item, ex);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(item, ex);
            }
        }

        /// <summary>
        /// Скачивает файл во временный .tmp файл, проверяет валидность декодированием
        /// через BitmapDecoder, и лишь затем атомарно перемещает в целевой файл
        /// с корректным расширением (JPEG -> .jpg).
        /// </summary>
        private static async Task<bool> DownloadAndVerifyImageAsync(string url, string destPath)
        {
            var tempPath = Path.Combine(UserDataDir, "temp_" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                var tempDir = Path.GetDirectoryName(tempPath);
                if (!string.IsNullOrEmpty(tempDir))
                    Directory.CreateDirectory(tempDir);

                using var response = await Http.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return false;

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes == null || bytes.Length < 32)
                    return false;

                await File.WriteAllBytesAsync(tempPath, bytes);

                // Проверяем валидность файла декодированием через BitmapDecoder
                bool isValid = false;
                bool isJpeg = bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
                using (var stream = File.OpenRead(tempPath))
                {
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.None);
                    if (decoder.Frames.Count > 0 && decoder.Frames[0].PixelWidth > 1 && decoder.Frames[0].PixelHeight > 1)
                    {
                        isValid = true;
                    }
                }

                if (!isValid)
                {
                    if (File.Exists(tempPath)) File.Delete(tempPath);
                    return false;
                }

                // Если это JPEG — сохраняем с расширением .jpg
                var finalPath = isJpeg ? Path.ChangeExtension(destPath, ".jpg") : destPath;
                var destDir = Path.GetDirectoryName(finalPath);
                if (!string.IsNullOrEmpty(destDir))
                    Directory.CreateDirectory(destDir);

                if (File.Exists(finalPath))
                    File.Delete(finalPath);

                File.Move(tempPath, finalPath);
                return true;
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    try { File.Delete(tempPath); } catch { }
                }
                return false;
            }
        }

        private static void LogError(MediaItem item, Exception ex)
        {
            if (!AppConfig.Debug) return;
            try
            {
                var logPath = Path.Combine(UserDataDir, "poster_errors.log");
                Directory.CreateDirectory(UserDataDir);
                if (File.Exists(logPath) && new FileInfo(logPath).Length > 100 * 1024)
                {
                    File.Delete(logPath);
                }
                var line = $"[{DateTime.Now:HH:mm:ss}] {item.Title} ({item.Kind}): {ex.GetType().Name}: {ex.Message}{Environment.NewLine}";
                File.AppendAllText(logPath, line);
            }
            catch { }
        }

        private static void LogInfo(MediaItem item, string message)
        {
            if (!AppConfig.Debug) return;
            try
            {
                var logPath = Path.Combine(UserDataDir, "poster_errors.log");
                Directory.CreateDirectory(UserDataDir);
                if (File.Exists(logPath) && new FileInfo(logPath).Length > 100 * 1024)
                {
                    File.Delete(logPath);
                }
                var line = $"[{DateTime.Now:HH:mm:ss}] {item.Title} ({item.Kind}): {message}{Environment.NewLine}";
                File.AppendAllText(logPath, line);
            }
            catch { }
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
            bool ok = await DownloadAndVerifyImageAsync(posterUrl, destPath);
            if (ok)
                LogInfo(item, "OK: постер скачан с TMDb");
            return ok;
        }

        // ------------------------------------------------------------------
        // Open Library — обложки книг (поиск по ISBN, затем по TitleEn + Author)
        // ------------------------------------------------------------------
        private static async Task<bool> FetchBookCoverAsync(MediaItem item, string destPath)
        {
            // 1. Приоритетный поиск по ISBN через Covers API Open Library
            if (!string.IsNullOrWhiteSpace(item.Isbn))
            {
                var cleanIsbn = item.Isbn.Trim();
                var isbnUrl = $"https://covers.openlibrary.org/b/isbn/{cleanIsbn}-L.jpg?default=false";
                try
                {
                    if (await DownloadAndVerifyImageAsync(isbnUrl, destPath))
                    {
                        LogInfo(item, "OK: обложка скачана по ISBN с Open Library");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    LogInfo(item, $"OpenLibrary ISBN: {ex.Message}");
                }
            }

            // 2. Поиск по каталогу Open Library (по названию на английском или русском + автору)
            var searchTitle = !string.IsNullOrWhiteSpace(item.TitleEn) ? item.TitleEn : item.Title;
            var query = Uri.EscapeDataString(searchTitle);
            var authorParam = !string.IsNullOrWhiteSpace(item.Author) ? "&author=" + Uri.EscapeDataString(item.Author) : "";
            var searchUrl = $"https://openlibrary.org/search.json?title={query}{authorParam}&limit=5";

            try
            {
                var json = await Http.GetStringAsync(searchUrl);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("docs", out var docs) && docs.GetArrayLength() > 0)
                {
                    long? coverId = null;
                    foreach (var d in docs.EnumerateArray())
                    {
                        if (d.TryGetProperty("cover_i", out var c) && c.ValueKind == JsonValueKind.Number)
                        {
                            coverId = c.GetInt64();
                            break;
                        }
                    }

                    if (coverId != null)
                    {
                        var coverUrl = $"https://covers.openlibrary.org/b/id/{coverId}-L.jpg";
                        if (await DownloadAndVerifyImageAsync(coverUrl, destPath))
                        {
                            LogInfo(item, "OK: обложка скачана по cover_i с Open Library");
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogInfo(item, $"OpenLibrary search: {ex.Message}");
            }

            LogInfo(item, "OpenLibrary: обложка не найдена, используется стилизованная заглушка");
            return false;
        }

        // ------------------------------------------------------------------
        // Заглушка: спокойный тональный градиент в фирменной палитре
        // Для книг — крупное читаемое название и автор, для фильмов — монограмма
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

                if (item.Kind == MediaKind.Book)
                {
                    // Декоративная рамка для обложки книги
                    var borderPen = new Pen(new SolidColorBrush(Color.FromArgb(40, 242, 233, 228)), 1.5);
                    dc.DrawRectangle(null, borderPen, new Rect(16, 16, PosterW - 32, PosterH - 32));

                    // Крупное название книги
                    var titleBrush = new SolidColorBrush(Color.FromArgb(235, 242, 233, 228));
                    var titleText = new FormattedText(
                        item.Title,
                        System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(new FontFamily("Georgia, Times New Roman, Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                        22,
                        titleBrush,
                        VisualTreeHelper.GetDpi(visual).PixelsPerDip)
                    {
                        MaxTextWidth = PosterW - 48,
                        MaxTextHeight = PosterH / 2,
                        TextAlignment = TextAlignment.Center
                    };
                    dc.DrawText(titleText, new Point(24, PosterH * 0.30));

                    // Автор книги
                    if (!string.IsNullOrWhiteSpace(item.Author))
                    {
                        var authorBrush = new SolidColorBrush(Color.FromArgb(180, 242, 233, 228));
                        var authorText = new FormattedText(
                            item.Author,
                            System.Globalization.CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            new Typeface(new FontFamily("Segoe UI"), FontStyles.Italic, FontWeights.Normal, FontStretches.Normal),
                            15,
                            authorBrush,
                            VisualTreeHelper.GetDpi(visual).PixelsPerDip)
                        {
                            MaxTextWidth = PosterW - 48,
                            MaxTextHeight = 60,
                            TextAlignment = TextAlignment.Center
                        };
                        dc.DrawText(authorText, new Point(24, PosterH * 0.65));
                    }
                }
                else
                {
                    // Для фильмов: центрированная монограмма
                    var letter = string.IsNullOrWhiteSpace(item.Title) ? "?" : item.Title.Trim()[0].ToString().ToUpper();
                    var letterBrush = new SolidColorBrush(Color.FromArgb(70, 242, 233, 228));
                    var letterText = new FormattedText(
                        letter, System.Globalization.CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI Semibold"), 68, letterBrush,
                        VisualTreeHelper.GetDpi(visual).PixelsPerDip);
                    dc.DrawText(letterText, new Point((PosterW - letterText.Width) / 2, (PosterH - letterText.Height) / 2));
                }
            }

            var bmp = new RenderTargetBitmap(PosterW, PosterH, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(visual);

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bmp));
            var dir = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            using var stream = File.Create(destPath);
            encoder.Save(stream);
        }

        private static (Color top, Color bottom) ColorFor(string text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= 16777619u;
            }
            var seed = hash % 3;
            return seed switch
            {
                0 => (Color.FromRgb(0x2E, 0x23, 0x27), Color.FromRgb(0x22, 0x1A, 0x1E)),
                1 => (Color.FromRgb(0x2B, 0x21, 0x29), Color.FromRgb(0x22, 0x1A, 0x1E)),
                _ => (Color.FromRgb(0x29, 0x21, 0x24), Color.FromRgb(0x22, 0x1A, 0x1E)),
            };
        }
    }
}

