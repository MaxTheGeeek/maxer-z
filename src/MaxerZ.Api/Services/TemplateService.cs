using System;
using System.IO;
using System.Text.Json;

namespace MaxerZ.Api.Services
{
    public class Template
    {
        public string Language { get; set; } = "";
        public string HeaderImagePath { get; set; } = "";
        public string FooterImagePath { get; set; } = "";
        public string DateFormat { get; set; } = "";
        public string SalutationPrefix { get; set; } = "";
        public string ClosingLine { get; set; } = "";
    }

    public class TemplateService
    {
        public Template Load(string language)
        {
            var lang = (language ?? "en").ToLower();
            var filename = $"cover_letter_{lang}.json";
            var pathsToTry = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "..", "Resources", "Templates", filename),
                Path.Combine(AppContext.BaseDirectory, "Resources", "Templates", filename),
                Path.Combine(AppContext.BaseDirectory, filename),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Templates", filename),
                Path.Combine(Directory.GetCurrentDirectory(), "src", "MaxerZ.Maui", "Resources", "Templates", filename),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "MaxerZ.Maui", "Resources", "Templates", filename)
            };

            foreach (var path in pathsToTry)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        var json = File.ReadAllText(path);
                        var template = JsonSerializer.Deserialize<Template>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        if (template != null) return template;
                    }
                    catch { /* ignore and try next path */ }
                }
            }

            // Fallback hardcoded defaults if files are not found
            if (lang == "de")
            {
                return new Template
                {
                    Language = "de",
                    HeaderImagePath = "header_de.png",
                    FooterImagePath = "footer_de.png",
                    DateFormat = "d. MMMM yyyy",
                    SalutationPrefix = "Sehr geehrte",
                    ClosingLine = "Mit freundlichen Grüßen,"
                };
            }

            return new Template
            {
                Language = "en",
                HeaderImagePath = "header_en.png",
                FooterImagePath = "footer_en.png",
                DateFormat = "MMMM d, yyyy",
                SalutationPrefix = "Dear",
                ClosingLine = "Best regards,"
            };
        }

        public byte[] GetImageBytes(string imageName)
        {
            var pathsToTry = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "..", "Resources", "Templates", imageName),
                Path.Combine(AppContext.BaseDirectory, "Resources", "Templates", imageName),
                Path.Combine(AppContext.BaseDirectory, imageName),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Templates", imageName),
                Path.Combine(Directory.GetCurrentDirectory(), "src", "MaxerZ.Maui", "Resources", "Templates", imageName),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "MaxerZ.Maui", "Resources", "Templates", imageName)
            };

            foreach (var path in pathsToTry)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        return File.ReadAllBytes(path);
                    }
                    catch { }
                }
            }

            // Fallback: return a 1x1 transparent PNG to prevent QuestPDF from crashing if images are not on disk yet
            return new byte[] {
                0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
                0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
                0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
                0x0D, 0x49, 0x44, 0x41, 0x54, 0x18, 0x57, 0x63, 0x60, 0x60, 0x60, 0x60,
                0x00, 0x00, 0x00, 0x05, 0x00, 0x01, 0xA5, 0x67, 0x7D, 0x01, 0x00, 0x00,
                0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
            };
        }
    }
}
