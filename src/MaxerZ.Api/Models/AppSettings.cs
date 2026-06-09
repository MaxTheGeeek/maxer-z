using System.Collections.Generic;

namespace MaxerZ.Api.Models
{
    public class AppSettings
    {
        // === OpenRouter ===
        // Key presence determines if this provider is active.
        // If empty string → provider is auto-disabled.
        public string OpenRouterApiKey { get; set; } = "";

        // Ordered list of OpenRouter models to try.
        // If first model runs out of tokens, next is used automatically.
        public List<string> OpenRouterModelChain { get; set; } = new()
        {
            "openrouter/free",
            "meta-llama/llama-3-8b-instruct:free",
            "google/gemma-2-9b-it:free",
            "openchat/openchat-7b:free"
        };

        // === Groq ===
        // Free, fast, unlimited for practical purposes.
        // Auto-disabled if key is empty.
        public string GroqApiKey { get; set; } = "";
        public string GroqModel { get; set; } = "llama-3.1-8b-instant";

        // === Ollama (Homelab) ===
        // No key needed — disabled only if BaseUrl is empty.
        public string OllamaBaseUrl { get; set; } = "";
        public string OllamaModel { get; set; } = "mistral";

        // === Provider priority order ===
        // AI: do NOT change this default order.
        // OpenRouter first (many free models), Groq second (fast+free), Ollama last (homelab).
        public List<string> ProviderPriority { get; set; } = new()
        {
            "openrouter",
            "groq",
            "ollama"
        };

        // === App ===
        public string Theme { get; set; } = "dark";
        public string ExportDirectory { get; set; } = "~/Documents/MaxerZ";
        public UserProfile Profile { get; set; } = new();
    }

    public class UserProfile
    {
        private string _address = "Wiener Straße 20 / 1, 2442 Unterwaltersdorf";

        public string FullName { get; set; } = "Majid Behzadi";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string LinkedInUrl { get; set; } = "";
        public string GitHubUrl { get; set; } = "";
        public string WebsiteUrl { get; set; } = "";

        public string Address
        {
            get => string.IsNullOrWhiteSpace(_address) ? "Wiener Straße 20 / 1, 2442 Unterwaltersdorf" : _address;
            set => _address = value;
        }
    }
}
