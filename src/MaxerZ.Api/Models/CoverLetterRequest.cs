namespace MaxerZ.Api.Models
{
    public class CoverLetterRequest
    {
        public string CompanyName { get; set; } = "";
        public string? ContactPerson { get; set; }
        public string? Department { get; set; }
        public string CompanyLocation { get; set; } = "";
        public string Position { get; set; } = "";
        public string Language { get; set; } = "en"; // "en" | "de"
        public string CoverLetterBody { get; set; } = "";
    }
}
