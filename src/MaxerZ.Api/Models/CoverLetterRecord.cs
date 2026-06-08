using System;

namespace MaxerZ.Api.Models
{
    public class CoverLetterRecord
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = "";
        public string? ContactPerson { get; set; }
        public string? Department { get; set; }
        public string CompanyLocation { get; set; } = "";
        public string Position { get; set; } = "";
        public string Language { get; set; } = "en";
        public string ContentBody { get; set; } = "";
        public string PdfPath { get; set; } = "";
        public bool SyncedToMcp { get; set; } = false;
        public string UsedProvider { get; set; } = "";
        public string UsedModel { get; set; } = "";
        public string SelectedTemplate { get; set; } = "template_1";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "draft";
    }
}
