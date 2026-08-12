using System;
using System.ComponentModel.DataAnnotations;

namespace MaxerZ.Api.Models
{
    public class UserActivity
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; } = "";
        public string ActionType { get; set; } = ""; // "CoverLetter", "Resume", "AtsReview", "PdfMerge"
        public string Description { get; set; } = "";
        public string MetadataJson { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
