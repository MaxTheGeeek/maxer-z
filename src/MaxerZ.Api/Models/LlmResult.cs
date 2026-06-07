using System.Collections.Generic;

namespace MaxerZ.Api.Models
{
    /// <summary>
    /// Returned by LlmOrchestrator after successful layout validation.
    /// Always contains a result — either from LLM or from fallback parser.
    /// </summary>
    public class LlmResult
    {
        public string CompanyNameFormatted { get; set; } = "";
        public string PositionFormatted { get; set; } = "";
        public string SalutationLine { get; set; } = "";
        public List<string> BodyParagraphs { get; set; } = new();
        public string ClosingLine { get; set; } = "";
        public string SignerName { get; set; } = "";

        // Audit trail — which provider/model actually produced this
        public string UsedProvider { get; set; } = "fallback";
        public string UsedModel { get; set; } = "none";
        public List<string> AttemptLog { get; set; } = new(); // e.g. ["openrouter/mistral-7b → token limit", "groq/llama3 → ok"]
        public List<string> Warnings { get; set; } = new();
        public bool WasFallback { get; set; } = false; // true if all LLMs failed → raw text used
    }
}
