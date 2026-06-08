using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MaxerZ.Api.Models;
using MaxerZ.Api.Services.Providers;

namespace MaxerZ.Api.Services
{
    /// <summary>
    /// THE CORE SERVICE.
    ///
    /// Execution flow:
    /// 1. Build active provider list from ProviderPriority, skipping unconfigured ones.
    /// 2. Try providers in order.
    /// 3. If a provider throws ProviderExhaustedException → log it, try next.
    /// 4. If a provider throws ProviderUnavailableException → log it, try next.
    /// 5. If ALL providers fail → use RawFallbackLayout() so the user is never blocked.
    /// 6. Parse LLM response. If parsing fails but we have partial text → use partial.
    /// 7. Return LlmResult with full audit trail.
    ///
    /// KEY RULE: This method NEVER throws. It always returns an LlmResult.
    /// </summary>
    public class LlmOrchestrator
    {
        private readonly IEnumerable<ILlmProvider> _providers;
        private readonly SettingsService _settings;
        private readonly ILogger<LlmOrchestrator> _logger;

        public LlmOrchestrator(
            IEnumerable<ILlmProvider> providers,
            SettingsService settings,
            ILogger<LlmOrchestrator> logger)
        {
            _providers = providers;
            _settings = settings;
            _logger = logger;
        }

        public async Task<LlmResult> ValidateAndLayoutAsync(
            CoverLetterRequest request,
            CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(request.CoverLetterBody) &&
                request.CoverLetterBody.TrimStart().StartsWith("{"))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<LlmResult>(request.CoverLetterBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (parsed != null && parsed.BodyParagraphs != null && parsed.BodyParagraphs.Count > 0)
                    {
                        if (string.IsNullOrEmpty(parsed.CompanyNameFormatted))
                            parsed.CompanyNameFormatted = request.CompanyName;
                        if (string.IsNullOrEmpty(parsed.PositionFormatted))
                            parsed.PositionFormatted = request.Position;
                        
                        parsed.UsedProvider = "edited";
                        parsed.UsedModel = "manual";
                        parsed.WasFallback = false;
                        return parsed;
                    }
                }
                catch
                {
                    // Ignore and run normal flow
                }
            }

            var cfg = _settings.Get();
            var prompt = BuildPrompt(request);
            var attemptLog = new List<string>();

            // Build ordered, filtered provider list
            var activeProviders = cfg.ProviderPriority
                .Select(id => _providers.FirstOrDefault(p => p.ProviderId == id))
                .Where(p => p != null && p!.IsConfigured(cfg))
                .Cast<ILlmProvider>()
                .ToList();

            if (activeProviders.Count == 0)
            {
                _logger.LogWarning("No LLM providers configured. Using raw fallback.");
                attemptLog.Add("no-providers-configured → raw-fallback");
                return RawFallbackLayout(request, attemptLog,
                    new List<string> { "No API keys configured. Using raw text." });
            }

            foreach (var provider in activeProviders)
            {
                try
                {
                    _logger.LogInformation("Trying provider: {Provider}", provider.ProviderId);
                    var (rawResponse, modelUsed) = await provider.CompleteAsync(prompt, cfg, ct);

                    attemptLog.Add($"{provider.ProviderId}/{modelUsed} → ok");

                    var parsed = TryParseResponse(rawResponse, request);
                    if (parsed != null)
                    {
                        parsed.UsedProvider = provider.ProviderId;
                        parsed.UsedModel = modelUsed;
                        parsed.AttemptLog = attemptLog;
                        return parsed;
                    }

                    // Got a response but couldn't parse JSON → still usable as raw
                    attemptLog.Add($"{provider.ProviderId}/{modelUsed} → parse-failed, using raw");
                    return RawFallbackLayout(request, attemptLog,
                        new List<string> { "LLM response could not be parsed. Raw content used." },
                        provider.ProviderId, modelUsed);
                }
                catch (ProviderExhaustedException ex)
                {
                    var msg = $"{ex.Provider}/{ex.Model} → token-exhausted: {ex.Message}";
                    _logger.LogWarning(msg);
                    attemptLog.Add(msg);
                    // Continue to next provider — this is the key fallback logic
                }
                catch (ProviderUnavailableException ex)
                {
                    var msg = $"{ex.Provider} → unavailable: {ex.Message}";
                    _logger.LogWarning(msg);
                    attemptLog.Add(msg);
                    // Continue to next provider
                }
                catch (Exception ex)
                {
                    var msg = $"{provider.ProviderId} → unexpected: {ex.Message}";
                    _logger.LogError(ex, msg);
                    attemptLog.Add(msg);
                    // Continue to next provider — never crash
                }
            }

            // All providers failed
            _logger.LogWarning("All providers failed. Using raw fallback.");
            attemptLog.Add("all-providers-failed → raw-fallback");
            return RawFallbackLayout(request, attemptLog,
                new List<string> { "All LLM providers unavailable. Raw content used directly." });
        }

        private string BuildPrompt(CoverLetterRequest req)
        {
            var cfg = _settings.Get();
            if (req.Mode == "existing")
            {
                return $$"""
                You are a professional cover letter parser and formatter for MaxerZ app.
                The user has pasted their existing recipient info and cover letter. Your task is to clean it up, fix any typos, make clean paragraphs, and return it structured in JSON.

                STRICT RULES:
                - Do NOT rewrite or paraphrase the cover letter content. Keep the text as close to the input as possible, but you may fix minor spelling/grammar/punctuation errors.
                - Organize it into the JSON structure below.
                - Extract company name, target position, salutation, body paragraphs, closing, and signer.
                - Respond ONLY with a valid JSON object. No markdown. No backticks. No explanation.

                Required JSON structure:
                {
                  "companyNameFormatted": "string — company name extracted from info",
                  "positionFormatted": "string — job position title extracted from info",
                  "salutationLine": "string — salutation line (e.g., 'Sehr geehrter Herr Feichtegger,' or 'Dear Mr. Smith,')",
                  "bodyParagraphs": ["string", "string"],
                  "closingLine": "string — e.g. 'Mit freundlichen Grüßen,' or 'Best regards,'",
                  "signerName": "string — name of the sender at the end",
                  "warnings": []
                }

                Recipient Info Input:
                {{req.RawRecipientInfo}}

                Cover Letter Body Input:
                {{req.CoverLetterBody}}
                """;
            }
            else
            {
                return $$"""
                You are an expert ATS-friendly cover letter generator for MaxerZ app.
                Generate a professional cover letter in the requested language ({{req.Language}}) tailored to the provided job description and company details.

                Sender Profile:
                - Name: {{cfg.Profile.FullName}}
                - Email: {{cfg.Profile.Email}}
                - Phone: {{cfg.Profile.Phone}}
                - LinkedIn: {{cfg.Profile.LinkedInUrl}}
                - GitHub: {{cfg.Profile.GitHubUrl}}
                - Website/Portfolio: {{cfg.Profile.WebsiteUrl}}

                Job Details:
                - Company: {{req.CompanyName}}
                - Position: {{req.Position}}
                - Location: {{req.CompanyLocation}}
                - Contact Person: {{req.ContactPerson ?? "not provided"}}
                - Department: {{req.Department ?? "not provided"}}

                Job Description / Requirements:
                {{req.JobDescription}}

                STRICT RULES:
                - Tone must be professional, confident, and persuasive.
                - Do NOT include the sender's address/name or receiver's address inside the cover letter paragraphs, as these are rendered automatically by the PDF template layout.
                - The cover letter must have a proper salutation line. If the contact person is a name like 'Stefan Feichtegger', write a proper German/English salutation (e.g. 'Sehr geehrter Herr Feichtegger,' or 'Dear Mr. Feichtegger,').
                - Organize the generated text into the JSON structure below.
                - Respond ONLY with a valid JSON object. No markdown. No backticks. No explanation.

                Required JSON structure:
                {
                  "companyNameFormatted": "{{req.CompanyName}}",
                  "positionFormatted": "{{req.Position}}",
                  "salutationLine": "string — e.g. 'Sehr geehrter Herr Feichtegger,' or 'Dear Mr. Feichtegger,'",
                  "bodyParagraphs": ["paragraph 1", "paragraph 2", "paragraph 3"],
                  "closingLine": "string — e.g. 'Mit freundlichen Grüßen,' or 'Best regards,'",
                  "signerName": "{{cfg.Profile.FullName}}",
                  "warnings": []
                }
                """;
            }
        }

        private LlmResult? TryParseResponse(string raw, CoverLetterRequest fallback)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            try
            {
                var clean = raw
                    .Trim()
                    .TrimStart('`')
                    .TrimEnd('`')
                    .Replace("```json", "")
                    .Replace("```", "")
                    .Trim();

                // Find JSON object boundaries in case there's surrounding text
                var start = clean.IndexOf('{');
                var end = clean.LastIndexOf('}');
                if (start < 0 || end < 0 || end <= start) return null;
                clean = clean[start..(end + 1)];

                var parsed = JsonSerializer.Deserialize<LlmResult>(clean,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                // Validate minimum required fields
                if (parsed == null ||
                    string.IsNullOrEmpty(parsed.CompanyNameFormatted) ||
                    parsed.BodyParagraphs.Count == 0)
                    return null;

                return parsed;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Last-resort fallback — parses the raw cover letter body with simple heuristics.
        /// Always produces a usable result. The user is never blocked.
        /// </summary>
        private LlmResult RawFallbackLayout(
            CoverLetterRequest req,
            List<string> attemptLog,
            List<string> warnings,
            string usedProvider = "fallback",
            string usedModel = "none")
        {
            var cfg = _settings.Get();
            var company = req.CompanyName;
            var pos = req.Position;

            if (req.Mode == "existing" && !string.IsNullOrWhiteSpace(req.RawRecipientInfo))
            {
                var infoLines = req.RawRecipientInfo
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0)
                    .ToList();

                if (infoLines.Count > 0) company = infoLines[0];
                if (infoLines.Count > 3) pos = infoLines[3];
            }

            var lines = req.CoverLetterBody
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            var salutation = lines.FirstOrDefault(l =>
                l.StartsWith("Dear", StringComparison.OrdinalIgnoreCase) ||
                l.StartsWith("Sehr geehrte", StringComparison.OrdinalIgnoreCase) ||
                l.StartsWith("Sehr geehrter", StringComparison.OrdinalIgnoreCase)) ?? "";

            var closing = lines.LastOrDefault(l =>
                l.Contains("regards", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("freundlichen", StringComparison.OrdinalIgnoreCase)) ?? "";

            var signer = lines.LastOrDefault(l =>
                l.Contains("Behzadi", StringComparison.OrdinalIgnoreCase) ||
                l.Contains("Majid", StringComparison.OrdinalIgnoreCase)) ?? cfg.Profile.FullName;

            // Body = everything that is not salutation, closing, or signer
            var skip = new HashSet<string> { salutation, closing, signer };
            var body = lines
                .Where(l => !skip.Contains(l))
                .ToList();

            return new LlmResult
            {
                CompanyNameFormatted = company,
                PositionFormatted = pos,
                SalutationLine = salutation,
                BodyParagraphs = body,
                ClosingLine = closing.Length > 0 ? closing :
                    (req.Language == "de" ? "Mit freundlichen Grüßen," : "Best regards,"),
                SignerName = signer,
                UsedProvider = usedProvider,
                UsedModel = usedModel,
                AttemptLog = attemptLog,
                Warnings = warnings,
                WasFallback = usedProvider == "fallback"
            };
        }
    }
}
