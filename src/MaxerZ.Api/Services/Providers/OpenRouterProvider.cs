using System;
using System.Collections.Generic;
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
    /// OpenRouter provider with internal model chain fallback.
    /// If model A runs out of tokens → tries model B from the chain → model C, etc.
    /// Only throws ProviderExhaustedException after ALL models in the chain are exhausted.
    /// </summary>
    public class OpenRouterProvider : ILlmProvider
    {
        private readonly IHttpClientFactory _http;
        public string ProviderId => "openrouter";

        public OpenRouterProvider(IHttpClientFactory http) => _http = http;

        public bool IsConfigured(AppSettings s) =>
            !string.IsNullOrWhiteSpace(s.OpenRouterApiKey);

        public async Task<(string response, string modelUsed)> CompleteAsync(
            string prompt, AppSettings settings, CancellationToken ct)
        {
            var models = settings.OpenRouterModelChain;
            if (models == null || models.Count == 0)
                throw new ProviderExhaustedException(ProviderId, "none", "No models configured");

            var attemptedModels = new List<string>();
            Exception? lastError = null;

            foreach (var model in models)
            {
                if (string.IsNullOrWhiteSpace(model))
                    continue;

                attemptedModels.Add(model);
                try
                {
                    var result = await TryModelAsync(prompt, model, settings, ct);
                    return (result, model);
                }
                catch (ProviderExhaustedException ex)
                {
                    // This model hit token limit → try next model in chain
                    lastError = ex;
                    continue;
                }
                catch (HttpRequestException ex) when (
                    ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                    ex.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
                {
                    // Rate limit or quota on this model → try next
                    lastError = ex;
                    continue;
                }
                // Any other exception (auth, network) → propagate immediately
            }

            // All models exhausted
            throw new ProviderExhaustedException(
                ProviderId,
                string.Join(" → ", attemptedModels),
                $"All OpenRouter models exhausted: {lastError?.Message}");
        }

        private static string SanitizeApiKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "";
            return key.Trim()
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", "")
                .Replace("\u200B", "");
        }

        private async Task<string> TryModelAsync(
            string prompt, string model, AppSettings settings, CancellationToken ct)
        {
            var client = _http.CreateClient("openrouter");
            var apiKey = SanitizeApiKey(settings.OpenRouterApiKey);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Add("HTTP-Referer", "app://maxerz");
            client.DefaultRequestHeaders.Add("X-Title", "MaxerZ");

            var body = new
            {
                model,
                messages = new[] { new { role = "user", content = prompt } },
                temperature = 0.1,
                max_tokens = 900
            };

            HttpResponseMessage resp;
            try
            {
                resp = await client.PostAsJsonAsync(
                    "https://openrouter.ai/api/v1/chat/completions", body, ct);
            }
            catch (HttpRequestException ex)
            {
                throw new ProviderUnavailableException(ProviderId, ex.Message);
            }

            // Check for token/rate errors before reading body
            if (resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                resp.StatusCode == System.Net.HttpStatusCode.PaymentRequired)
            {
                throw new ProviderExhaustedException(ProviderId, model,
                    $"HTTP {(int)resp.StatusCode} from OpenRouter on model {model}");
            }

            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new ProviderUnavailableException(ProviderId, "OpenRouter: invalid API key");
            }

            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(
                cancellationToken: ct);

            // Check for finish_reason: "length" → means token limit hit mid-response
            var choices = json.GetProperty("choices");
            if (choices.GetArrayLength() == 0)
            {
                throw new ProviderUnavailableException(ProviderId, $"OpenRouter returned empty choices for model {model}");
            }

            var choice = choices[0];
            string? finishReason = null;
            if (choice.TryGetProperty("finish_reason", out var frProp))
            {
                finishReason = frProp.GetString();
            }

            string content = "";
            if (choice.TryGetProperty("message", out var msgProp) &&
                msgProp.TryGetProperty("content", out var contentProp))
            {
                content = contentProp.GetString() ?? "";
            }

            if (finishReason == "length" && string.IsNullOrWhiteSpace(content))
            {
                // Completely cut off → try next model
                throw new ProviderExhaustedException(ProviderId, model,
                    $"finish_reason=length, no usable content from {model}");
            }

            // finish_reason=length but we have partial content → still usable
            // Return what we have; orchestrator will parse it
            return content;
        }
    }
}
