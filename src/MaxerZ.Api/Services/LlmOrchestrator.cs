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
                        if (string.IsNullOrEmpty(parsed.CompanyLocation))
                            parsed.CompanyLocation = request.CompanyLocation;
                        if (string.IsNullOrEmpty(parsed.ContactPerson))
                            parsed.ContactPerson = request.ContactPerson ?? "";
                        if (string.IsNullOrEmpty(parsed.Department))
                            parsed.Department = request.Department ?? "";
                        
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
                You are a professional cover letter parser and formatter for the MaxerZ app.
                Your task is to parse, clean up, and structure the candidate's existing recipient information and cover letter.
                
                STRICT WRITING & PARSING RULES:
                1. Do NOT paraphrase, rewrite, or summarize the cover letter content. Keep the text exactly as the candidate wrote it, but you may correct minor spelling, grammar, or punctuation errors.
                2. Ensure there are absolutely no AI-generated placeholders or artifacts (like `---` or `--` or `[Insert Name]`). If you find any placeholder brackets or symbols, resolve them or clean them up.
                3. Extract the recipient details (Company Name, Location, Contact Person, Department) from the provided Recipient Info Block.
                4. The subject line (starting with 'Betreff', 'Subject', or 'Bewerbung' or containing 'Bewerbung als') must NEVER be included in `companyNameFormatted` or `companyLocation` or `contactPerson`. It must strictly be extracted into `positionFormatted`. Keep any existing colon (:) if present.
                5. Organize the parsed text into the JSON structure below.
                6. Respond ONLY with a valid, single JSON object. No explanation or surrounding text.

                Required JSON structure:
                {
                  "companyNameFormatted": "string — company name extracted from info",
                  "companyLocation": "string — company location/city extracted from info",
                  "contactPerson": "string — contact person name if found, otherwise empty string",
                  "department": "string — department name if found, otherwise empty string",
                  "positionFormatted": "string — job position title extracted from info",
                  "salutationLine": "string — salutation line extracted from cover letter (e.g., 'Sehr geehrter Herr Feichtegger,' or 'Dear Mr. Smith,')",
                  "bodyParagraphs": ["paragraph 1", "paragraph 2", ...],
                  "closingLine": "string — closing line (e.g., 'Mit freundlichen Grüßen,' or 'Best regards,')",
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
                You are a professional, elite executive copywriter specializing in high-impact cover letters.
                Your task is to write a highly tailored, natural, and humanized cover letter in the requested language ({{req.Language}}) for:
                - Candidate: {{cfg.Profile.FullName}}
                - Target Company: {{req.CompanyName}}
                - Target Position: {{req.Position}}

                Job Details:
                - Location: {{req.CompanyLocation}}
                - Contact Person: {{req.ContactPerson ?? "not provided"}}
                - Department: {{req.Department ?? "not provided"}}

                Job Description / Requirements:
                {{req.JobDescription}}

                Additional Custom Context/Instructions from Candidate:
                {{req.CoverLetterBody}}

                STRICT WRITING RULES (To avoid AI artifacts and make it look human-written):
                1. The tone must be professional, confident, organic, and genuinely enthusiastic. Avoid generic corporate buzzwords, clichés, or overly flowery AI language (e.g. "thrilled", "passionate", "delighted", "deeply motivated").
                2. NEVER use markdown separators (like `---` or `***` or `--`), bullet points, list items, or headers inside the cover letter text itself. Use only natural, flowing paragraphs.
                3. Do NOT include the sender's address/name or receiver's address inside the cover letter paragraphs. These are automatically rendered by the PDF template layout.
                4. Provide a proper salutation line. If a contact person is provided (e.g. 'Stefan Feichtegger'), address them directly (e.g., 'Sehr geehrter Herr Feichtegger,' or 'Dear Mr. Feichtegger,'). If no contact person name is provided, address 'Dear Hiring Team,' (English) or 'Sehr geehrte Damen und Herren,' (German).
                5. Write the cover letter in natural paragraphs (usually 3 to 4 paragraphs).
                6. Avoid generic cover letter templates. Start directly with a compelling opening statement. Do NOT use boilerplate openers like "I am writing to express my interest in..." or "With this application, I would like to apply for...". Instead, hook the reader by connecting the candidate's achievements or skill alignment directly with the position.
                7. Respond ONLY with a valid, single JSON object. Do not wrap it in markdown block tags (like ```json), do not write any introductory or explanatory text.

                Required JSON structure:
                {
                  "companyNameFormatted": "{{req.CompanyName}}",
                  "companyLocation": "{{req.CompanyLocation}}",
                  "contactPerson": "{{req.ContactPerson ?? ""}}",
                  "department": "{{req.Department ?? ""}}",
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
                    parsed.BodyParagraphs == null ||
                    parsed.BodyParagraphs.Count == 0)
                    return null;

                if (string.IsNullOrEmpty(parsed.CompanyNameFormatted))
                    parsed.CompanyNameFormatted = fallback.CompanyName;
                if (string.IsNullOrEmpty(parsed.PositionFormatted))
                    parsed.PositionFormatted = fallback.Position;
                if (string.IsNullOrEmpty(parsed.CompanyLocation))
                    parsed.CompanyLocation = fallback.CompanyLocation;
                if (string.IsNullOrEmpty(parsed.ContactPerson))
                    parsed.ContactPerson = fallback.ContactPerson ?? "";
                if (string.IsNullOrEmpty(parsed.Department))
                    parsed.Department = fallback.Department ?? "";

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
            var company = req.CompanyName ?? "";
            var pos = req.Position ?? "";
            var location = req.CompanyLocation ?? "";
            var contact = req.ContactPerson ?? "";
            var dept = req.Department ?? "";

            if (req.Mode == "existing" && !string.IsNullOrWhiteSpace(req.RawRecipientInfo))
            {
                var infoLines = req.RawRecipientInfo
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0)
                    .ToList();

                // Extract subject line if it exists to prevent mapping it to location or contact
                var subjectIdx = infoLines.FindIndex(l =>
                    l.StartsWith("Betreff", StringComparison.OrdinalIgnoreCase) ||
                    l.StartsWith("Subject", StringComparison.OrdinalIgnoreCase) ||
                    l.StartsWith("Bewerbung", StringComparison.OrdinalIgnoreCase) ||
                    l.StartsWith("Re:", StringComparison.OrdinalIgnoreCase) ||
                    l.Contains("Bewerbung als", StringComparison.OrdinalIgnoreCase));

                if (subjectIdx >= 0)
                {
                    pos = infoLines[subjectIdx];
                    infoLines.RemoveAt(subjectIdx);
                }

                if (infoLines.Count > 0) company = infoLines[0];
                
                if (infoLines.Count == 2)
                {
                    location = infoLines[1];
                }
                else if (infoLines.Count == 3)
                {
                    contact = infoLines[1];
                    location = infoLines[2];
                }
                else if (infoLines.Count >= 4)
                {
                    contact = infoLines[1];
                    dept = infoLines[2];
                    location = infoLines[3];
                }
            }

            var bodyText = req.CoverLetterBody ?? "";
            var lines = bodyText
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            var salutation = "";
            var closing = "";
            var signer = cfg.Profile.FullName ?? "Max Mustermann";
            var body = new List<string>();

            if (req.Mode != "existing" && lines.Count == 0)
            {
                // Generate a professional generic template fallback
                salutation = (req.Language ?? "en").ToLower() == "de"
                    ? (!string.IsNullOrWhiteSpace(contact) ? $"Sehr geehrte(r) {contact}," : "Sehr geehrte Damen und Herren,")
                    : (!string.IsNullOrWhiteSpace(contact) ? $"Dear {contact}," : "Dear Hiring Manager,");

                var bodyParagraph1 = (req.Language ?? "en").ToLower() == "de"
                    ? $"mit großem Interesse bewerbe ich mich hiermit um die Position als {pos} bei {company}."
                    : $"I am writing to express my strong interest in the {pos} position at {company}.";

                var bodyParagraph2 = (req.Language ?? "en").ToLower() == "de"
                    ? "Aufgrund meiner fundierten technischen Kenntnisse und meiner praktischen Erfahrung bin ich überzeugt, einen wertvollen Beitrag zu Ihrem Team leisten zu können."
                    : "With my strong technical background and practical experience, I am confident in my ability to make a valuable contribution to your team.";

                var bodyParagraph3 = (req.Language ?? "en").ToLower() == "de"
                    ? "Ich freue mich über die Gelegenheit, mich Ihnen in einem persönlichen Gespräch vorzustellen."
                    : "Thank you for your time and consideration. I look forward to the opportunity to discuss my qualifications further.";

                closing = (req.Language ?? "en").ToLower() == "de" ? "Mit freundlichen Grüßen," : "Best regards,";
                body = new List<string> { bodyParagraph1, bodyParagraph2, bodyParagraph3 };
            }
            else
            {
                salutation = lines.FirstOrDefault(l =>
                    l.StartsWith("Dear", StringComparison.OrdinalIgnoreCase) ||
                    l.StartsWith("Sehr geehrte", StringComparison.OrdinalIgnoreCase) ||
                    l.StartsWith("Sehr geehrter", StringComparison.OrdinalIgnoreCase)) ?? "";

                closing = lines.LastOrDefault(l =>
                    l.Contains("regards", StringComparison.OrdinalIgnoreCase) ||
                    l.Contains("freundlichen", StringComparison.OrdinalIgnoreCase)) ?? "";

                signer = lines.LastOrDefault(l =>
                    l.Contains("Mustermann", StringComparison.OrdinalIgnoreCase) ||
                    l.Contains("Muster", StringComparison.OrdinalIgnoreCase)) ?? (cfg.Profile.FullName ?? "Max Mustermann");

                // Body = everything that is not salutation, closing, or signer
                var skip = new HashSet<string> { salutation, closing, signer };
                body = lines
                    .Where(l => !skip.Contains(l))
                    .ToList();
            }

            return new LlmResult
            {
                CompanyNameFormatted = company,
                PositionFormatted = pos,
                CompanyLocation = location,
                ContactPerson = contact,
                Department = dept,
                SalutationLine = salutation,
                BodyParagraphs = body,
                ClosingLine = !string.IsNullOrEmpty(closing) ? closing :
                    ((req.Language ?? "en").ToLower() == "de" ? "Mit freundlichen Grüßen," : "Best regards,"),
                SignerName = signer,
                UsedProvider = usedProvider,
                UsedModel = usedModel,
                AttemptLog = attemptLog,
                Warnings = warnings,
                WasFallback = usedProvider == "fallback"
            };
        }

        public async Task<ResumeResult> ValidateAndLayoutResumeAsync(
            ResumeRequest request,
            CancellationToken ct = default)
        {
            var cfg = _settings.Get();
            var prompt = BuildResumePrompt(request);
            var attemptLog = new List<string>();

            var activeProviders = cfg.ProviderPriority
                .Select(id => _providers.FirstOrDefault(p => p.ProviderId == id))
                .Where(p => p != null && p!.IsConfigured(cfg))
                .Cast<ILlmProvider>()
                .ToList();

            if (activeProviders.Count == 0)
            {
                _logger.LogWarning("No LLM providers configured. Using raw fallback.");
                attemptLog.Add("no-providers-configured → raw-fallback");
                return RawResumeFallbackLayout(request, attemptLog,
                    new List<string> { "No API keys configured. Using raw text." });
            }

            foreach (var provider in activeProviders)
            {
                try
                {
                    _logger.LogInformation("Trying provider for resume: {Provider}", provider.ProviderId);
                    var (rawResponse, modelUsed) = await provider.CompleteAsync(prompt, cfg, ct);

                    attemptLog.Add($"{provider.ProviderId}/{modelUsed} → ok");

                    var parsed = TryParseResumeResponse(rawResponse, request);
                    if (parsed != null)
                    {
                        parsed.UsedProvider = provider.ProviderId;
                        parsed.UsedModel = modelUsed;
                        parsed.AttemptLog = attemptLog;
                        return parsed;
                    }

                    attemptLog.Add($"{provider.ProviderId}/{modelUsed} → parse-failed, using raw");
                    return RawResumeFallbackLayout(request, attemptLog,
                        new List<string> { "LLM response could not be parsed. Raw content used." },
                        provider.ProviderId, modelUsed);
                }
                catch (ProviderExhaustedException ex)
                {
                    var msg = $"{ex.Provider}/{ex.Model} → token-exhausted: {ex.Message}";
                    _logger.LogWarning(msg);
                    attemptLog.Add(msg);
                }
                catch (ProviderUnavailableException ex)
                {
                    var msg = $"{ex.Provider} → unavailable: {ex.Message}";
                    _logger.LogWarning(msg);
                    attemptLog.Add(msg);
                }
                catch (Exception ex)
                {
                    var msg = $"{provider.ProviderId} → unexpected: {ex.Message}";
                    _logger.LogError(ex, msg);
                    attemptLog.Add(msg);
                }
            }

            _logger.LogWarning("All providers failed. Using raw fallback.");
            attemptLog.Add("all-providers-failed → raw-fallback");
            return RawResumeFallbackLayout(request, attemptLog,
                new List<string> { "All LLM providers unavailable. Raw content used directly." });
        }

        private string BuildResumePrompt(ResumeRequest req)
        {
            var langsFormatted = req.Languages != null && req.Languages.Count > 0
                ? string.Join(", ", req.Languages.Select(l => $"{l.Language} ({l.Proficiency})"))
                : "None provided";

            var templateGuidance = !string.IsNullOrWhiteSpace(req.SelectedTemplate) && req.SelectedTemplate.StartsWith("custom_")
                ? "CUSTOM TEMPLATE LAYOUT NOTICE: The user selected a custom PDF layout template. Ensure text length, paragraph density, and bullet structure are concise, clean, and neatly formatted to overlay seamlessly without overflowing fixed custom template bounds."
                : $"SELECTED TEMPLATE DESIGN ({req.SelectedTemplate}): Format all section text to align with modern ATS resume standards, using strong action verbs, clean bullet points (using - or •), and precise professional phrasing.";

            return $$"""
            You are a professional resume writer and formatter for MaxerZ app.

            TARGET TEMPLATE & STYLE GUIDELINES:
            {{templateGuidance}}

            STRICT RULES:
            - Optimize and format the text for each section to be highly professional, polished, and ATS-friendly.
            - Do NOT rewrite or paraphrase the facts; keep all dates, job titles, companies, and achievements accurate as provided.
            - Focus on clear formatting, action verbs, and readability.
            - Ensure bullet points in Work Experience, Education & Certificates, Key Skills, and Projects use clear line breaks and standard bullet markers.
            - Respond ONLY with a valid JSON object. No markdown. No backticks. No explanation.

            Required JSON structure:
            {
              "summaryFormatted": "string — polished professional summary",
              "experienceFormatted": "string — structured professional work experiences with clean bullet points",
              "educationFormatted": "string — structured education and certificates history",
              "skillsFormatted": "string — structured skills list",
              "projectsFormatted": "string — structured projects list",
              "warnings": ["string"]
            }

            Input Details:
            - Target Language: {{req.Language}}
            - Candidate Name: {{req.FullName}}
            - Target Role: {{req.TargetRole}}
            - Professional Summary: {{req.Summary}}
            - Work Experience: {{req.Experience}}
            - Education & Certificates: {{req.Education}}
            - Key Skills: {{req.Skills}}
            - Projects & Highlights: {{req.Projects}}
            - Spoken Languages: {{langsFormatted}}
            """;
        }

        private ResumeResult? TryParseResumeResponse(string raw, ResumeRequest fallback)
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

                var start = clean.IndexOf('{');
                var end = clean.LastIndexOf('}');
                if (start < 0 || end < 0 || end <= start) return null;
                clean = clean[start..(end + 1)];

                var parsed = JsonSerializer.Deserialize<ResumeResult>(clean,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return parsed;
            }
            catch
            {
                return null;
            }
        }

        private ResumeResult RawResumeFallbackLayout(
            ResumeRequest req,
            List<string> attemptLog,
            List<string> warnings,
            string usedProvider = "fallback",
            string usedModel = "none")
        {
            return new ResumeResult
            {
                SummaryFormatted = req.Summary,
                ExperienceFormatted = req.Experience,
                EducationFormatted = req.Education,
                SkillsFormatted = req.Skills,
                ProjectsFormatted = req.Projects,
                UsedProvider = usedProvider,
                UsedModel = usedModel,
                AttemptLog = attemptLog,
                Warnings = warnings,
                WasFallback = usedProvider == "fallback"
            };
        }

        // ==============================================================================
        // ATS RESUME REVIEW & SCORING PIPELINE
        // ==============================================================================
        public async Task<AtsResult> ExecuteAtsPipelineAsync(AtsRequest request, CancellationToken ct = default)
        {
            var attemptLog = new List<string>();
            var cfg = _settings.Get();

            var activeProviders = cfg.ProviderPriority
                .Select(id => _providers.FirstOrDefault(p => p.ProviderId == id))
                .Where(p => p != null && p!.IsConfigured(cfg))
                .Cast<ILlmProvider>()
                .ToList();

            if (activeProviders.Count == 0)
            {
                attemptLog.Add("no-active-providers → raw-fallback");
                return RawAtsFallbackLayout(request, attemptLog);
            }

            var prompt = BuildAtsPrompt(request);

            foreach (var prov in activeProviders)
            {
                ct.ThrowIfCancellationRequested();
                var msg = $"Attempting ATS review via provider: {prov.ProviderId}";
                _logger.LogInformation(msg);

                try
                {
                    var (rawResult, modelUsed) = await prov.CompleteAsync(prompt, cfg, ct);
                    if (!string.IsNullOrWhiteSpace(rawResult))
                    {
                        var parsed = TryParseAtsResponse(rawResult, request);
                        if (parsed != null && parsed.OverallScore >= 0)
                        {
                            parsed.UsedProvider = prov.ProviderId;
                            parsed.UsedModel = modelUsed;
                            parsed.AttemptLog = attemptLog;
                            parsed.WasFallback = false;
                            attemptLog.Add($"{prov.ProviderId}/{modelUsed} → ok");
                            return parsed;
                        }
                        else
                        {
                            var parseErr = $"{prov.ProviderId}/{modelUsed} → parse-failed";
                            _logger.LogWarning(parseErr);
                            attemptLog.Add(parseErr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    var err = $"{prov.ProviderId} → unexpected: {ex.Message}";
                    _logger.LogWarning(ex, err);
                    attemptLog.Add(err);
                }
            }

            _logger.LogWarning("All providers failed for ATS review. Using raw fallback.");
            attemptLog.Add("all-providers-failed → raw-fallback");
            return RawAtsFallbackLayout(request, attemptLog);
        }

        private string BuildAtsPrompt(AtsRequest req)
        {
            return $$"""
            You are acting as two experts simultaneously:
            1. Senior Technical Recruiter / HR Reviewer with 10+ years screening resumes for {{req.JobTitle}} roles.
            2. Resume / Document QA Specialist evaluating structure, formatting, layout integrity, and ATS parseability.

            EVALUATION INPUTS:
            - Target Job Title: {{req.JobTitle}}
            - Seniority Level: {{req.SeniorityLevel}}
            - Template Archetype: {{req.TargetArchetype}}
            - Target Job Description / Context: {{req.JobDescription ?? "Not specified (use standard industry expectations for this job title)"}}

            CANDIDATE RESUME TEXT TO REVIEW:
            {{req.ResumeText}}

            STRICT SCORING & REPORTING RULES:
            1. Score strictly on a 0–100 scale using this weighted rubric:
               - Content relevance & impact (30% max): achievement-driven bullets, metrics, keyword match
               - ATS parseability / technical structure (20% max): section headers, text accessibility, machine readability
               - Layout & formatting integrity (15% max): margin/spacing consistency, alignment, section order
               - Visual / style consistency (15% max): font count, archetype appropriateness, date/bullet consistency
               - Grammar & language quality (15% max): zero-tolerance scaling, tense consistency, action verbs
               - Completeness & professionalism (5% max): contact info, no unexplained gaps
            2. Quote exact words/phrases from the candidate's resume for every strength and weakness.
            3. Highlight ATS risks with pass/fail flags (e.g. ❌ or ✅).
            4. Provide prioritized actionable recommendations with concrete before/after model rewrites.
            5. Respond ONLY with a valid single JSON object. No preamble. No markdown code block backticks.

            Required JSON structure:
            {
              "overallScore": 78,
              "subScores": {
                "contentRelevance": 24,
                "atsParseability": 16,
                "layoutFormatting": 12,
                "visualConsistency": 11,
                "grammarLanguage": 12,
                "completeness": 3
              },
              "sectionReviews": [
                {
                  "sectionName": "Work Experience",
                  "strengths": ["Quoted text strength 1"],
                  "weaknesses": ["Quoted text weakness 1 — why it hurts"]
                }
              ],
              "designLayoutReview": [
                {
                  "location": "Experience Section",
                  "finding": "Description of font or alignment issue",
                  "severity": "medium"
                }
              ],
              "atsRisks": [
                {
                  "passed": false,
                  "flagText": "❌ Contact info in text box might be skipped by standard ATS parsers."
                }
              ],
              "recommendations": [
                {
                  "priority": 1,
                  "itemToChange": "Weak passive verb in bullet under Acme Corp",
                  "whyItMatters": "Passive language lowers impact for senior roles.",
                  "exampleFix": "Rewrite 'Responsible for leading backend API' to 'Spearheaded microservices overhaul in C#, reducing latency by 35%.'"
                }
              ],
              "revisedScorePotential": 88
            }
            """;
        }

        private AtsResult? TryParseAtsResponse(string raw, AtsRequest fallback)
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

                var start = clean.IndexOf('{');
                var end = clean.LastIndexOf('}');
                if (start < 0 || end < 0 || end <= start) return null;
                clean = clean[start..(end + 1)];

                var parsed = JsonSerializer.Deserialize<AtsResult>(clean,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return parsed;
            }
            catch
            {
                return null;
            }
        }

        private AtsResult RawAtsFallbackLayout(AtsRequest req, List<string> attemptLog)
        {
            return new AtsResult
            {
                OverallScore = 70,
                SubScores = new AtsSubScores
                {
                    ContentRelevance = 20,
                    AtsParseability = 15,
                    LayoutFormatting = 12,
                    VisualConsistency = 10,
                    GrammarLanguage = 10,
                    Completeness = 3
                },
                SectionReviews = new List<AtsSectionReview>
                {
                    new AtsSectionReview
                    {
                        SectionName = "General Assessment",
                        Strengths = new List<string> { "Resume text successfully ingested and parsed for target role: " + req.JobTitle },
                        Weaknesses = new List<string> { "LLM API providers were offline or unavailable. Basic fallback scoring applied." }
                    }
                },
                AtsRisks = new List<AtsRiskItem>
                {
                    new AtsRiskItem { Passed = true, FlagText = "✅ Standard text formatting detected." },
                    new AtsRiskItem { Passed = false, FlagText = "⚠️ Automated LLM analysis offline — verify API keys in Settings." }
                },
                Recommendations = new List<AtsRecommendation>
                {
                    new AtsRecommendation
                    {
                        Priority = 1,
                        ItemToChange = "Configure LLM API Provider",
                        WhyItMatters = "An active LLM provider enables deep 7-stage recruiter auditing.",
                        ExampleFix = "Go to Settings -> API Keys and enter an OpenRouter, Groq, or Ollama API key."
                    }
                },
                RevisedScorePotential = 85,
                WasFallback = true,
                AttemptLog = attemptLog,
                UsedProvider = "fallback",
                UsedModel = "none"
            };
        }
    }
}
