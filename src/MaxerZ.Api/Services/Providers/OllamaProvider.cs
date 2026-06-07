using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MaxerZ.Api.Models;

namespace MaxerZ.Api.Services.Providers
{
    /// <summary>
    /// Ollama — homelab fallback. No key needed.
    /// Disabled if OllamaBaseUrl is empty.
    /// </summary>
    public class OllamaProvider : ILlmProvider
    {
        private readonly IHttpClientFactory _http;
        public string ProviderId => "ollama";

        public OllamaProvider(IHttpClientFactory http) => _http = http;

        public bool IsConfigured(AppSettings s) =>
            !string.IsNullOrWhiteSpace(s.OllamaBaseUrl);

        public async Task<(string response, string modelUsed)> CompleteAsync(
            string prompt, AppSettings settings, CancellationToken ct)
        {
            var client = _http.CreateClient("ollama");
            // Longer timeout for local models
            client.Timeout = TimeSpan.FromSeconds(60);

            var body = new
            {
                model = settings.OllamaModel,
                prompt,
                stream = false
            };

            HttpResponseMessage resp;
            try
            {
                resp = await client.PostAsJsonAsync(
                    $"{settings.OllamaBaseUrl.TrimEnd('/')}/api/generate", body, ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new ProviderUnavailableException(ProviderId,
                    $"Ollama unreachable at {settings.OllamaBaseUrl}: {ex.Message}");
            }

            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);

            return (json.GetProperty("response").GetString() ?? "", settings.OllamaModel);
        }
    }
}
