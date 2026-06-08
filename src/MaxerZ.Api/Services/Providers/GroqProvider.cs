using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MaxerZ.Api.Models;

namespace MaxerZ.Api.Services.Providers
{
    /// <summary>
    /// Groq — fast, free, reliable. Single model per call.
    /// OpenAI-compatible endpoint.
    /// </summary>
    public class GroqProvider : ILlmProvider
    {
        private readonly IHttpClientFactory _http;
        public string ProviderId => "groq";

        public GroqProvider(IHttpClientFactory http) => _http = http;

        public bool IsConfigured(AppSettings s) =>
            !string.IsNullOrWhiteSpace(s.GroqApiKey);

        private static string SanitizeApiKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "";
            return key.Trim()
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", "")
                .Replace("\u200B", "");
        }

        public async Task<(string response, string modelUsed)> CompleteAsync(
            string prompt, AppSettings settings, CancellationToken ct)
        {
            var client = _http.CreateClient("groq");
            var apiKey = SanitizeApiKey(settings.GroqApiKey);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            var body = new
            {
                model = settings.GroqModel,
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0.1,
                max_tokens = 900
            };

            HttpResponseMessage resp;
            try
            {
                resp = await client.PostAsJsonAsync(
                    "https://api.groq.com/openai/v1/chat/completions", body, ct);
            }
            catch (HttpRequestException ex)
            {
                throw new ProviderUnavailableException(ProviderId, ex.Message);
            }

            if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                throw new ProviderExhaustedException(ProviderId, settings.GroqModel,
                    "Groq rate limit hit");

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new ProviderUnavailableException(ProviderId, "Groq: invalid API key");

            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);

            var choices = json.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
                throw new ProviderUnavailableException(ProviderId, "Groq returned empty choices");

            var content = choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            return (content, settings.GroqModel);
        }
    }
}
