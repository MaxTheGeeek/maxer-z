using System.Collections.Generic;

namespace MaxerZ.Api.Models
{
    public class AtsSubScores
    {
        public int ContentRelevance { get; set; } // max 30
        public int AtsParseability { get; set; } // max 20
        public int LayoutFormatting { get; set; } // max 15
        public int VisualConsistency { get; set; } // max 15
        public int GrammarLanguage { get; set; } // max 15
        public int Completeness { get; set; } // max 5
    }

    public class AtsSectionReview
    {
        public string SectionName { get; set; } = "";
        public List<string> Strengths { get; set; } = new();
        public List<string> Weaknesses { get; set; } = new();
    }

    public class AtsDesignFinding
    {
        public string Location { get; set; } = "";
        public string Finding { get; set; } = "";
        public string Severity { get; set; } = "medium"; // "high" | "medium" | "low"
    }

    public class AtsRiskItem
    {
        public bool Passed { get; set; }
        public string FlagText { get; set; } = "";
    }

    public class AtsRecommendation
    {
        public int Priority { get; set; }
        public string ItemToChange { get; set; } = "";
        public string WhyItMatters { get; set; } = "";
        public string ExampleFix { get; set; } = "";
    }

    public class AtsResult
    {
        public int OverallScore { get; set; } // 0 - 100
        public AtsSubScores SubScores { get; set; } = new();
        public List<AtsSectionReview> SectionReviews { get; set; } = new();
        public List<AtsDesignFinding> DesignLayoutReview { get; set; } = new();
        public List<AtsRiskItem> AtsRisks { get; set; } = new();
        public List<AtsRecommendation> Recommendations { get; set; } = new();
        public int RevisedScorePotential { get; set; }
        
        public bool WasFallback { get; set; }
        public List<string> AttemptLog { get; set; } = new();
        public string UsedProvider { get; set; } = "";
        public string UsedModel { get; set; } = "";
    }
}
