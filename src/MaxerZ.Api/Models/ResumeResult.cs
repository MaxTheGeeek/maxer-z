using System.Collections.Generic;

namespace MaxerZ.Api.Models
{
    public class ResumeResult
    {
        public string SummaryFormatted { get; set; } = "";
        public string ExperienceFormatted { get; set; } = "";
        public string EducationFormatted { get; set; } = "";
        public string SkillsFormatted { get; set; } = "";
        public string ProjectsFormatted { get; set; } = "";

        // Audit Trail
        public string UsedProvider { get; set; } = "fallback";
        public string UsedModel { get; set; } = "none";
        public List<string> AttemptLog { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public bool WasFallback { get; set; } = false;
    }
}
