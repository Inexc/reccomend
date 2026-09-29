using System;
using System.IO;
using System.Text.Json;

namespace MovieBookRecommender.Models
{
    public class AppSettings
    {
        public string Theme { get; set; } = "Dark";
        public string Scale { get; set; } = "Normal"; // Compact, Normal, Large
        public double WindowWidth { get; set; } = 1400;
        public double WindowHeight { get; set; } = 850;
        public bool IsMaximized { get; set; } = false;
    }
}
