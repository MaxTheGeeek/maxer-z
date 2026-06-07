using System;
using System.Collections.Generic;
using System.Linq;
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

        public SettingsController(SettingsService settings)
        {
            _settings = settings;
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

        /// <summary>
        /// Test a specific provider with a minimal prompt.
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

            // Temporarily apply settings for this test only
            var original = _settings.Get();
            _settings.Save(tempSettings);

            try
            {
                var provider = providers.FirstOrDefault(p => p.ProviderId.Equals(providerId, StringComparison.OrdinalIgnoreCase));
                if (provider == null)
                    return Ok(new { success = false, error = $"Provider '{providerId}' not found" });

                if (!provider.IsConfigured(tempSettings))
                    return Ok(new { success = false, error = "Provider not configured (missing key/URL)" });

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
            finally
            {
                // Restore original settings — save new ones only if user clicks Save
                _settings.Save(original);
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
    }
}
