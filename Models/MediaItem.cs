using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MovieBookRecommender.Models
{
    /// <summary>
    /// Единая модель для фильма и книги — данные почти идентичны
    /// (название, автор/режиссёр, жанры, год, рейтинг, описание, постер),
    /// поэтому один класс используется для обоих типов, а тип элемента
    /// определяется тем, из какого списка (Movies/Books) он пришёл.
    /// </summary>
    public class MediaItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("author")]
        public string Author { get; set; } = "";

        [JsonPropertyName("genre")]
        public List<string> Genre { get; set; } = new();

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("rating")]
        public double Rating { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("poster")]
        public string Poster { get; set; } = "";

        /// <summary>Не сериализуется — тип проставляется программно после загрузки.</summary>
        [JsonIgnore]
        public MediaKind Kind { get; set; }

        [JsonIgnore]
        public string GenreText => string.Join(", ", Genre);
    }

    public enum MediaKind
    {
        Movie,
        Book
    }
}
