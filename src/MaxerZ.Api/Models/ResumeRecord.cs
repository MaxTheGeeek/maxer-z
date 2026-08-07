using System;

namespace MaxerZ.Api.Models
{
    public class ResumeRecord
    {
        public int Id { get; set; }
        
        // Raw inputs
        public string Summary { get; set; } = "";
        public string Experience { get; set; } = "";
        public string Education { get; set; } = "";
        public string Skills { get; set; } = "";
        public string Projects { get; set; } = "";
        
        // Metadata & settings
        public string Language { get; set; } = "en";
        public string SelectedTemplate { get; set; } = "template_1";
        public string HeaderAddress { get; set; } = "";
        public string PdfPath { get; set; } = "";
        public bool SyncedToMcp { get; set; } = false;
        
        // Provider audit trail
        public string UsedProvider { get; set; } = "";
        public string UsedModel { get; set; } = "";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "draft"; // "draft" | "exported"
    }
}
