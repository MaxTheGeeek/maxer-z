namespace MaxerZ.Api.Models
{
    public class CoverLetterRequest
    {
        public string Mode { get; set; } = "existing"; // "existing" | "generate"
        public string? RawRecipientInfo { get; set; }
        public string? JobDescription { get; set; }
        public string CompanyName { get; set; } = "";
        public string? ContactPerson { get; set; }
        public string? Department { get; set; }
        public string CompanyLocation { get; set; } = "";
        public string Position { get; set; } = "";
        public string Language { get; set; } = "en"; // "en" | "de"
        public string SelectedTemplate { get; set; } = "template_1"; // "template_1" | "template_2"
        public string CoverLetterBody { get; set; } = "";
    }
}
