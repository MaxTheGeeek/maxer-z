namespace MaxerZ.Api.Models
{
    public class ResumeRequest
    {
        public string Summary { get; set; } = "";
        public string Experience { get; set; } = "";
        public string Education { get; set; } = "";
        public string Skills { get; set; } = "";
        public string Projects { get; set; } = "";
        
        public string Language { get; set; } = "en"; // "en" | "de"
        public string SelectedTemplate { get; set; } = "template_1";
        public string HeaderAddress { get; set; } = "";
    }
}
