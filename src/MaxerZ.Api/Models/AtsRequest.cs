using System.Collections.Generic;

namespace MaxerZ.Api.Models
{
    public class AtsRequest
    {
        public string ResumeText { get; set; } = "";
        public string JobTitle { get; set; } = "";
        public string? JobDescription { get; set; } = "";
        public string SeniorityLevel { get; set; } = "mid"; // "entry" | "mid" | "senior" | "lead"
        public string TargetArchetype { get; set; } = "technical"; // "corporate" | "technical" | "creative" | "academic" | "sales"
    }
}
