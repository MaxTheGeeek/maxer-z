using System.Collections.Generic;

namespace MaxerZ.Api.Models
{
    public class LanguageItem
    {
        public string Language { get; set; } = "";
        public string Proficiency { get; set; } = "";
    }

    public class ResumeRequest
    {
        public string FullName { get; set; } = "";
        public string TargetRole { get; set; } = "";

        public string Summary { get; set; } = "";
        public string Experience { get; set; } = "";
        public string Education { get; set; } = "";
        public string Skills { get; set; } = "";
        public string Projects { get; set; } = "";
        
        public List<LanguageItem>? Languages { get; set; } = new();
        public List<string>? SectionOrder { get; set; } = new();

        public string Language { get; set; } = "en"; // "en" | "de"
        public string SelectedTemplate { get; set; } = "template_1";
        public string HeaderAddress { get; set; } = "";
    }
}
