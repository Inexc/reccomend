using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MovieBookRecommender.Models;

namespace MovieBookRecommender.Services
{
    /// <summary>
    /// Отвечает за загрузку фильмов/книг из Data/*.json и за хранение
    /// избранного (Data/favorites.json). Единая точка доступа к данным
    /// приложения — аналог utils/data_manager.py из Python-версии.
    /// </summary>
    public class DataManager
    {
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly string DataDir = Path.Combine(BaseDir, "Data");
        private static readonly string MoviesFile = Path.Combine(DataDir, "movies.json");
        private static readonly string BooksFile = Path.Combine(DataDir, "books.json");
        private static readonly string LegacyFavoritesFile = Path.Combine(DataDir, "favorites.json");
        public static readonly string UserDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MovieBookRecommender");
        private static readonly string FavoritesFile = Path.Combine(UserDataDir, "favorites.json");

        public List<MediaItem> Movies { get; private set; } = new();
        public List<MediaItem> Books { get; private set; } = new();
        public HashSet<string> Favorites { get; private set; } = new();

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
        };

        public void Load()
        {
            Movies = LoadItems(MoviesFile, MediaKind.Movie);
            Books = LoadItems(BooksFile, MediaKind.Book);
            Favorites = LoadFavorites();
        }

        private static List<MediaItem> LoadItems(string path, MediaKind kind)
        {
            if (!File.Exists(path))
                return new List<MediaItem>();

            try
            {
                var json = File.ReadAllText(path);
                var items = JsonSerializer.Deserialize<List<MediaItem>>(json) ?? new List<MediaItem>();
                foreach (var item in items)
                    item.Kind = kind;
                return items;
            }
            catch (Exception)
            {
                return new List<MediaItem>();
            }
        }

        private static HashSet<string> LoadFavorites()
        {
            try
            {
                Directory.CreateDirectory(UserDataDir);
                if (!File.Exists(FavoritesFile) && File.Exists(LegacyFavoritesFile))
                {
                    try { File.Copy(LegacyFavoritesFile, FavoritesFile, overwrite: true); } catch { }
                }

                if (!File.Exists(FavoritesFile))
                    return new HashSet<string>();

                var json = File.ReadAllText(FavoritesFile);
                var ids = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
                return new HashSet<string>(ids);
            }
            catch (Exception)
            {
                return new HashSet<string>();
            }
        }

        private void SaveFavorites()
        {
            try
            {
                Directory.CreateDirectory(UserDataDir);
                var json = JsonSerializer.Serialize(Favorites.OrderBy(x => x).ToList(), JsonOptions);
                File.WriteAllText(FavoritesFile, json);
            }
            catch
            {
                // Игнорируем ошибки записи, предотвращая сбои приложения
            }
        }

        public IEnumerable<MediaItem> AllItems() => Movies.Concat(Books);

        public MediaItem? GetById(string id) => AllItems().FirstOrDefault(i => i.Id == id);

        public List<string> GenresFor(IEnumerable<MediaItem> items)
        {
            var set = new SortedSet<string>();
            foreach (var item in items)
                foreach (var g in item.Genre)
                    set.Add(g);
            return set.ToList();
        }

        public static string NormalizeSearchText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            return text.Trim().ToLowerInvariant().Replace('ё', 'е');
        }

        public List<MediaItem> Search(string? query, IEnumerable<MediaItem>? pool = null)
        {
            var source = (pool ?? AllItems()).ToList();
            if (string.IsNullOrWhiteSpace(query))
                return source;

            var normalizedQuery = NormalizeSearchText(query);
            var words = normalizedQuery.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return source;

            return source.Where(item =>
            {
                var haystack = NormalizeSearchText($"{item.Title} {item.TitleEn} {item.Author} {item.GenreText} {item.Year}");
                foreach (var word in words)
                {
                    if (!haystack.Contains(word))
                        return false;
                }
                return true;
            }).ToList();
        }

        public bool IsFavorite(string id) => Favorites.Contains(id);

        public void ToggleFavorite(string id)
        {
            if (!Favorites.Remove(id))
                Favorites.Add(id);
            SaveFavorites();
        }

        public void RemoveFavorite(string id)
        {
            Favorites.Remove(id);
            SaveFavorites();
        }

        public void ClearFavorites()
        {
            Favorites.Clear();
            SaveFavorites();
        }

        public List<MediaItem> GetFavoriteItems() =>
            AllItems().Where(i => Favorites.Contains(i.Id)).ToList();
    }
}
