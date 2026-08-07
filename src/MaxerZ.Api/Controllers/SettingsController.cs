using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using MaxerZ.Api.Models;
using MaxerZ.Api.Services;
using MaxerZ.Api.Services.Providers;

namespace MaxerZ.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly SettingsService _settings;
        private readonly IHttpClientFactory _http;

        public SettingsController(SettingsService settings, IHttpClientFactory http)
        {
            _settings = settings;
            _http = http;
        }

        [HttpGet]
        public IActionResult Get() => Ok(_settings.Get());

        [HttpPost]
        public IActionResult Save([FromBody] AppSettings s)
        {
            if (s == null) return BadRequest("Settings cannot be null.");
            _settings.Save(s);
            return Ok(new { success = true });
        }

        [HttpGet("mcp")]
        public IActionResult GetMcp() => Ok(_settings.GetMcpConfig());

        [HttpPost("mcp")]
        public IActionResult SaveMcp([FromBody] McpConfig c)
        {
            if (c == null) return BadRequest("MCP Config cannot be null.");
            _settings.SaveMcpConfig(c);
            return Ok(new { success = true });
        }

        /// <summary>
        /// Returns only providers that currently have valid credentials.
        /// AI: Angular uses this response to show/hide provider sections in UI.
        /// </summary>
        [HttpGet("active-providers")]
        public IActionResult GetActiveProviders(
            [FromServices] IEnumerable<ILlmProvider> providers)
        {
            var cfg = _settings.Get();
            var active = providers
                .Where(p => p.IsConfigured(cfg))
                .Select(p => new
                {
                    id = p.ProviderId,
                    label = p.ProviderId switch
                    {
                        "openrouter" => "OpenRouter",
                        "groq" => "Groq",
                        "ollama" => $"Ollama ({cfg.OllamaBaseUrl})",
                        _ => p.ProviderId
                    }
                })
                .ToList();
            return Ok(new { providers = active, priority = cfg.ProviderPriority });
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

        /// <summary>
        /// Test a specific provider with a minimal prompt / metadata query (matching Smartiz).
        /// Returns detailed result including which model was used.
        /// AI: never save settings before testing — test with what's provided.
        /// </summary>
        [HttpPost("test-provider/{providerId}")]
        public async Task<IActionResult> TestProvider(
            string providerId,
            [FromBody] AppSettings tempSettings,
            [FromServices] IEnumerable<ILlmProvider> providers,
            CancellationToken ct)
        {
            if (tempSettings == null) return BadRequest("Settings cannot be null.");

            try
            {
                var provider = providers.FirstOrDefault(p => p.ProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase));
                if (provider == null)
                    return Ok(new { success = false, error = $"Provider '{providerId}' not found" });

                if (!provider.IsConfigured(tempSettings))
                    return Ok(new { success = false, error = "Provider not configured (missing key/URL)" });

                if (providerId.Equals("openrouter", StringComparison.OrdinalIgnoreCase))
                {
                    var client = _http.CreateClient("openrouter");
                    var apiKey = SanitizeApiKey(tempSettings.OpenRouterApiKey);
                    
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    if (!client.DefaultRequestHeaders.Contains("HTTP-Referer"))
                        client.DefaultRequestHeaders.Add("HTTP-Referer", "app://maxerz");
                    if (!client.DefaultRequestHeaders.Contains("X-Title"))
                        client.DefaultRequestHeaders.Add("X-Title", "MaxerZ");

                    var res = await client.GetAsync("https://openrouter.ai/api/v1/key", ct);
                    if (res.IsSuccessStatusCode)
                    {
                        var chain = tempSettings.OpenRouterModelChain ?? new List<string>();
                        var primaryModel = chain.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "openrouter/free";
                        return Ok(new { success = true, model = primaryModel, response = "Connected" });
                    }
                    else
                    {
                        var errText = await res.Content.ReadAsStringAsync(ct);
                        return Ok(new { success = false, error = $"OpenRouter rejected key: {res.StatusCode} - {errText}" });
                    }
                }
                else if (providerId.Equals("groq", StringComparison.OrdinalIgnoreCase))
                {
                    var client = _http.CreateClient("groq");
                    var apiKey = SanitizeApiKey(tempSettings.GroqApiKey);
                    
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                    var res = await client.GetAsync("https://api.groq.com/openai/v1/models", ct);
                    if (res.IsSuccessStatusCode)
                    {
                        return Ok(new { success = true, model = tempSettings.GroqModel ?? "llama-3.1-8b-instant", response = "Connected" });
                    }
                    else
                    {
                        var errText = await res.Content.ReadAsStringAsync(ct);
                        return Ok(new { success = false, error = $"Groq rejected key: {res.StatusCode} - {errText}" });
                    }
                }
                else if (providerId.Equals("ollama", StringComparison.OrdinalIgnoreCase))
                {
                    var client = _http.CreateClient("ollama");
                    var baseUrl = tempSettings.OllamaBaseUrl ?? "";

                    var res = await client.GetAsync($"{baseUrl.TrimEnd('/')}/api/tags", ct);
                    if (res.IsSuccessStatusCode)
                    {
                        return Ok(new { success = true, model = tempSettings.OllamaModel ?? "mistral", response = "Connected" });
                    }
                    else
                    {
                        return Ok(new { success = false, error = $"Ollama returned status code: {res.StatusCode}" });
                    }
                }

                // Fallback completion test
                var testPrompt = """
                    Respond ONLY with this exact JSON, nothing else:
                    {"status":"ok","provider":"test"}
                    """;

                var (response, model) = await provider.CompleteAsync(
                    testPrompt, tempSettings, ct);

                return Ok(new { success = true, model, response });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("test-mcp")]
        public async Task<IActionResult> TestMcp([FromServices] McpService mcp)
        {
            var test = new CoverLetterRecord
            {
                CompanyName = "MaxerZ-test",
                Position = "connectivity-check",
                Language = "en",
                ContentBody = "MCP connectivity test from MaxerZ"
            };
            var ok = await mcp.SaveCoverLetterAsync(test);
            return Ok(new { success = ok });
        }

        [HttpGet("templates")]
        public IActionResult GetTemplates()
        {
            var list = new List<object>
            {
                new { id = "template_1", name = "Template 1 (Professional Classic)", isCustom = false },
                new { id = "template_2", name = "Template 2 (Modern Minimalist)", isCustom = false }
            };

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MaxerZ", "Templates");

            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "*.pdf");
                foreach (var file in files)
                {
                    var filename = Path.GetFileName(file);
                    list.Add(new
                    {
                        id = filename,
                        name = Path.GetFileNameWithoutExtension(filename),
                        isCustom = true
                    });
                }
            }

            return Ok(list);
        }

        [HttpPost("templates/upload")]
        public async Task<IActionResult> UploadTemplate([FromForm] Microsoft.AspNetCore.Http.IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (Path.GetExtension(file.FileName).ToLower() != ".pdf")
                return BadRequest("Only PDF files are allowed as templates.");

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MaxerZ", "Templates");

            Directory.CreateDirectory(dir);

            var filePath = Path.Combine(dir, file.FileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Ok(new { success = true });
        }

        [HttpDelete("templates/{id}")]
        public IActionResult DeleteTemplate(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id == "template_1" || id == "template_2")
                return BadRequest("Invalid template ID.");

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MaxerZ", "Templates");

            var filename = Path.GetFileName(id);
            var filePath = Path.Combine(dir, filename);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
                return Ok(new { success = true });
            }

            return NotFound("Template not found.");
        }

        [HttpPost("clear-cache")]
        public IActionResult ClearCache()
        {
            try
            {
                // Clear static memory buffers in controllers
                CoverLetterController.LastPreviewPdf = null;
                ResumeController.LastPreviewPdf = null;

                // Clear temp folder files starting with CoverLetter_ or Resume_
                var tempPath = Path.GetTempPath();
                int count = 0;
                if (Directory.Exists(tempPath))
                {
                    var files = Directory.GetFiles(tempPath)
                        .Where(f => {
                            var name = Path.GetFileName(f);
                            return name.StartsWith("CoverLetter_", StringComparison.OrdinalIgnoreCase) || 
                                   name.StartsWith("Resume_", StringComparison.OrdinalIgnoreCase);
                        });

                    foreach (var file in files)
                    {
                        try
                        {
                            System.IO.File.Delete(file);
                            count++;
                        }
                        catch { /* Ignore locked files */ }
                    }
                }

                return Ok(new { success = true, message = $"Cleared in-memory buffers and deleted {count} temporary files." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, error = ex.Message });
            }
        }
    }
}
