using System;
using System.IO;
using System.Text.Json;

namespace MovieBookRecommender.Models
{
    public class AppSettings
    {
        public string Theme { get; set; } = "Тёмная (Тёплый нуар)";
        public string Scale { get; set; } = "Обычный (100%)";
        public double WindowWidth { get; set; } = 1400;
        public double WindowHeight { get; set; } = 850;
        public bool IsMaximized { get; set; } = false;
        public bool AutoSave { get; set; } = true;
        public bool EnableAnimations { get; set; } = true;
    }
}

