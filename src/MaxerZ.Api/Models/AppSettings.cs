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
            "moonshotai/kimi-k3",
            "openai/gpt-4o",
            "openai/gpt-5",
            "deepseek/deepseek-chat",
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
        private string _address = "Musterstraße 1, 1010 Wien";

        public string FullName { get; set; } = "Max Mustermann";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string LinkedInUrl { get; set; } = "";
        public string GitHubUrl { get; set; } = "";
        public string WebsiteUrl { get; set; } = "";
        
        public string Role { get; set; } = "";
        public string FooterText { get; set; } = "";

        public string Address
        {
            get => string.IsNullOrWhiteSpace(_address) ? "Musterstraße 1, 1010 Wien" : _address;
            set => _address = value;
        }

        public List<string> Addresses { get; set; } = new();
    }
}
