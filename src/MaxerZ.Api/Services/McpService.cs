using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MaxerZ.Api.Models;

namespace MaxerZ.Api.Services
{
    public class McpService
    {
        private readonly SettingsService _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<McpService> _logger;

        public McpService(
            SettingsService settings,
            IHttpClientFactory httpClientFactory,
            ILogger<McpService> logger)
        {
            _settings = settings;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<bool> SaveCoverLetterAsync(CoverLetterRecord record)
        {
            var config = _settings.GetMcpConfig();
            if (config == null || !config.IsEnabled || string.IsNullOrWhiteSpace(config.McpBaseUrl))
            {
                return false;
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);

                if (!string.IsNullOrWhiteSpace(config.McpApiKey))
                {
                    client.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.McpApiKey);
                }

                // POST to the configured MCP endpoint (we append /coverletters to the base URL)
                var url = $"{config.McpBaseUrl.TrimEnd('/')}/coverletters";
                var response = await client.PostAsJsonAsync(url, record);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Successfully synced cover letter {RecordId} to MCP.", record.Id);
                    return true;
                }

                _logger.LogWarning("MCP sync returned status code: {StatusCode}", response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync cover letter to MCP (silent failure)");
                return false;
            }
        }
    }
}
