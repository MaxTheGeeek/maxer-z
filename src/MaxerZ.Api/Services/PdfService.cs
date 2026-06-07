using System;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using MaxerZ.Api.Models;

namespace MaxerZ.Api.Services
{
    public class PdfService
    {
        private readonly SettingsService _settings;
        private readonly TemplateService _templateService;

        public PdfService(SettingsService settings, TemplateService templateService)
        {
            _settings = settings;
            _templateService = templateService;
        }

        public byte[] GeneratePdf(CoverLetterRequest request, LlmResult layout)
        {
            var tmpl = _templateService.Load(request.Language);
            var culture = (request.Language ?? "en").ToLower() == "de"
                ? new System.Globalization.CultureInfo("de-AT")
                : System.Globalization.CultureInfo.InvariantCulture;
            var today = DateTime.Now.ToString(tmpl.DateFormat, culture);

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(0);

                    page.Header()
                        .Height(120)
                        .Image(_templateService.GetImageBytes(tmpl.HeaderImagePath));

                    page.Content()
                        .PaddingHorizontal(42)
                        .PaddingTop(18)
                        .PaddingBottom(18)
                        .Column(col =>
                        {
                            col.Spacing(0);

                            // Company block + Date row
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(left =>
                                {
                                    left.Item()
                                        .Text(layout.CompanyNameFormatted)
                                        .FontFamily("Helvetica Neue").FontSize(11)
                                        .Bold().FontColor(Color.FromHex("#1A1A2E"));

                                    if (!string.IsNullOrWhiteSpace(request.ContactPerson))
                                        left.Item()
                                            .Text(request.ContactPerson!)
                                            .FontFamily("Helvetica Neue").FontSize(10)
                                            .FontColor(Color.FromHex("#555555"));

                                    if (!string.IsNullOrWhiteSpace(request.Department))
                                        left.Item()
                                            .Text(request.Department!)
                                            .FontFamily("Helvetica Neue").FontSize(10)
                                            .FontColor(Color.FromHex("#555555"));

                                    left.Item()
                                        .Text(request.CompanyLocation)
                                        .FontFamily("Helvetica Neue").FontSize(10)
                                        .FontColor(Color.FromHex("#555555"));
                                });

                                row.AutoItem().AlignRight()
                                    .Text(today)
                                    .FontFamily("Helvetica Neue").FontSize(10)
                                    .FontColor(Color.FromHex("#444444"));
                            });

                            col.Item().PaddingTop(18);

                            // Position (sky blue)
                            col.Item()
                                .Text($"Re: {layout.PositionFormatted}")
                                .FontFamily("Helvetica Neue").FontSize(10.5f)
                                .Bold().FontColor(Color.FromHex("#5BC0F8"));

                            col.Item().PaddingTop(14);

                            // Salutation
                            col.Item()
                                .Text(layout.SalutationLine)
                                .FontFamily("Helvetica Neue").FontSize(10.5f)
                                .FontColor(Color.FromHex("#1A1A2E"));

                            col.Item().PaddingTop(10);

                            // Body paragraphs
                            foreach (var para in layout.BodyParagraphs
                                .Where(p => !string.IsNullOrWhiteSpace(p)))
                            {
                                col.Item()
                                    .Text(para)
                                    .FontFamily("Helvetica Neue").FontSize(10.5f)
                                    .LineHeight(1.4f)
                                    .FontColor(Color.FromHex("#1A1A2E"));
                                col.Item().PaddingTop(8);
                            }

                            col.Item().PaddingTop(16);

                            // Closing
                            col.Item()
                                .Text(layout.ClosingLine)
                                .FontFamily("Helvetica Neue").FontSize(10.5f)
                                .FontColor(Color.FromHex("#1A1A2E"));

                            col.Item().PaddingTop(30);

                            // Signer name
                            col.Item()
                                .Text(layout.SignerName)
                                .FontFamily("Helvetica Neue").FontSize(10.5f)
                                .Bold().FontColor(Color.FromHex("#1A1A2E"));
                        });

                    page.Footer()
                        .Height(80)
                        .Image(_templateService.GetImageBytes(tmpl.FooterImagePath));
                });
            }).GeneratePdf();
        }

        public string SavePdf(byte[] bytes, string companyName)
        {
            var cfg = _settings.Get();
            var dir = cfg.ExportDirectory
                .Replace("~", Environment.GetFolderPath(
                    Environment.SpecialFolder.Personal));
            var expandedDir = Environment.ExpandEnvironmentVariables(dir);
            Directory.CreateDirectory(expandedDir);

            var safe = string.Concat(companyName.Split(Path.GetInvalidFileNameChars()));
            var name = $"CoverLetter_{safe}_{DateTime.Now:yyyyMMdd}.pdf";
            var path = Path.Combine(expandedDir, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }
    }
}
