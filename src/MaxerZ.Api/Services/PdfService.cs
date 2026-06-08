using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using PdfSharpCore.Drawing;
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
            // Find and load the selected template PDF
            var templatePath = FindTemplatePdfPath(request.SelectedTemplate);
            
            // Open the template PDF using PdfSharpCore
            using var document = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);
            if (document.PageCount == 0)
            {
                throw new InvalidOperationException("The template PDF contains no pages.");
            }

            // Retrieve the first page to draw text on
            var page = document.Pages[0];
            using var gfx = XGraphics.FromPdfPage(page);

            // Define colors matching the JSON template style
            var brushPrimary = new XSolidBrush(XColor.FromArgb(0x1A, 0x1A, 0x2E));
            var brushSecondary = new XSolidBrush(XColor.FromArgb(0x55, 0x55, 0x55));
            var brushAccent = new XSolidBrush(XColor.FromArgb(0x00, 0x6C, 0xA5));

            // Wipe pre-baked header area on the template page
            gfx.DrawRectangle(XBrushes.White, 0, 0, 595, 120);

            // Define header fonts & brushes
            var headerNameFont = new XFont("Arial", 17, XFontStyle.Bold);
            var headerRoleFont = new XFont("Arial", 10.5, XFontStyle.Regular);
            var headerInfoFont = new XFont("Arial", 8.5, XFontStyle.Regular);
            var brushHeaderBlue = new XSolidBrush(XColor.FromArgb(0x00, 0x6C, 0xA5));
            var brushHeaderDark = new XSolidBrush(XColor.FromArgb(0x33, 0x33, 0x33));

            // Line 1: Name
            DrawCenteredSegments(gfx, 50, new TextSegment
            {
                Text = "MAJID BEHZADI",
                Font = headerNameFont,
                Brush = brushHeaderBlue
            });

            // Line 2: Role / Skills
            DrawCenteredSegments(gfx, 68, new TextSegment
            {
                Text = "Full-Stack Engineer | C# & ASP.NET Core | TypeScript & JavaScript",
                Font = headerRoleFont,
                Brush = brushHeaderDark
            });

            // Line 3: Contact Details
            DrawCenteredSegments(gfx, 83, 
                new TextSegment { Text = "+43 6769701820 | maxbehzadi82@gmail.com | ", Font = headerInfoFont, Brush = brushHeaderDark },
                new TextSegment { Text = "linkedin.com/in/maxii", Font = headerInfoFont, Brush = brushHeaderBlue },
                new TextSegment { Text = " | Portfolio: ", Font = headerInfoFont, Brush = brushHeaderDark },
                new TextSegment { Text = "maxbehzadi.online", Font = headerInfoFont, Brush = brushHeaderBlue }
            );

            // Line 4: GitHub & Address
            var addressText = string.IsNullOrWhiteSpace(request.HeaderAddress) 
                ? (_settings.Get().Profile.Address ?? "Wiener Straße 20 / 1, 2442 Unterwaltersdorf")
                : request.HeaderAddress;

            DrawCenteredSegments(gfx, 98, 
                new TextSegment { Text = "github.com/MaxTheGeeek", Font = headerInfoFont, Brush = brushHeaderBlue },
                new TextSegment { Text = " | " + addressText, Font = headerInfoFont, Brush = brushHeaderDark }
            );

            // Define standard layout fonts
            var fontCompany = new XFont("Arial", 11, XFontStyle.Bold);
            var fontInfo = new XFont("Arial", 10, XFontStyle.Regular);
            var fontSubject = new XFont("Arial", 10.5, XFontStyle.Bold);
            var fontBody = new XFont("Arial", 10.5, XFontStyle.Regular);
            var fontSigner = new XFont("Arial", 10.5, XFontStyle.Bold);

            // Format the date based on language/template settings
            var tmpl = _templateService.Load(request.Language);
            var culture = (request.Language ?? "en").ToLower() == "de"
                ? new System.Globalization.CultureInfo("de-AT")
                : System.Globalization.CultureInfo.InvariantCulture;
            var today = DateTime.Now.ToString(tmpl.DateFormat, culture);

            // Content placement Y coordinate (below the header logo)
            double currentY = 130;

            // 1. Company name (bold, primary color)
            gfx.DrawString(layout.CompanyNameFormatted, fontCompany, brushPrimary, 42, currentY);
            currentY += 14;

            // 2. Contact person (regular, secondary color)
            if (!string.IsNullOrWhiteSpace(request.ContactPerson))
            {
                gfx.DrawString(request.ContactPerson, fontInfo, brushSecondary, 42, currentY);
                currentY += 12;
            }

            // 3. Department (regular, secondary color)
            if (!string.IsNullOrWhiteSpace(request.Department))
            {
                gfx.DrawString(request.Department, fontInfo, brushSecondary, 42, currentY);
                currentY += 12;
            }

            // 4. Company Location (regular, secondary color)
            gfx.DrawString(request.CompanyLocation, fontInfo, brushSecondary, 42, currentY);

            // 5. Date (drawn on the same line as Company Name, aligned right)
            var dateSize = gfx.MeasureString(today, fontInfo);
            gfx.DrawString(today, fontInfo, brushPrimary, 553 - dateSize.Width, 130);

            // 6. Subject Line / Position (bold, accent color)
            currentY += 35; // Spacing before subject line
            var posText = layout.PositionFormatted;
            if (!posText.StartsWith("Betreff", StringComparison.OrdinalIgnoreCase) && 
                !posText.StartsWith("Bewerbung", StringComparison.OrdinalIgnoreCase) && 
                !posText.StartsWith("Subject", StringComparison.OrdinalIgnoreCase) && 
                !posText.StartsWith("Re:", StringComparison.OrdinalIgnoreCase))
            {
                posText = request.Language == "de" ? $"Betreff: {posText}" : $"Subject: {posText}";
            }

            var wrappedSubject = WrapText(posText, 511, gfx, fontSubject);
            foreach (var line in wrappedSubject)
            {
                gfx.DrawString(line, fontSubject, brushAccent, 42, currentY);
                currentY += 14;
            }

            // 7. Salutation
            currentY += 20;
            gfx.DrawString(layout.SalutationLine, fontBody, brushPrimary, 42, currentY);
            currentY += 20;

            // 8. Body Paragraphs
            foreach (var para in layout.BodyParagraphs.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                var wrappedLines = WrapText(para, 511, gfx, fontBody);
                foreach (var line in wrappedLines)
                {
                    // Limit rendering if we overflow the printable area (above the footer)
                    if (currentY > 740)
                    {
                        break;
                    }
                    gfx.DrawString(line, fontBody, brushPrimary, 42, currentY);
                    currentY += 15; // Font size 10.5 with line-height
                }
                currentY += 10; // Paragraph spacing
            }

            // 9. Closing Line
            currentY += 10;
            if (currentY <= 740)
            {
                gfx.DrawString(layout.ClosingLine, fontBody, brushPrimary, 42, currentY);
                currentY += 30;
            }

            // 10. Signer Name
            if (currentY <= 740)
            {
                gfx.DrawString(layout.SignerName, fontSigner, brushPrimary, 42, currentY);
            }

            // Save PDF to memory stream and return as byte array
            using var ms = new MemoryStream();
            document.Save(ms);
            return ms.ToArray();
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

        private string FindTemplatePdfPath(string templateName)
        {
            var filename = templateName == "template_2" ? "coverletter_template_2.pdf" : "coverletter_template_1.pdf";
            var pathsToTry = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Resources", "Templates", filename),
                Path.Combine(AppContext.BaseDirectory, filename),
                Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Templates", filename),
                Path.Combine(Directory.GetCurrentDirectory(), "src", "MaxerZ.Maui", "Resources", "Templates", filename),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "MaxerZ.Maui", "Resources", "Templates", filename)
            };

            foreach (var path in pathsToTry)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            throw new FileNotFoundException($"Template PDF file {filename} not found.");
        }

        private List<string> WrapText(string text, double maxWidth, XGraphics gfx, XFont font)
        {
            var words = text.Split(' ');
            var lines = new List<string>();
            var currentLine = "";

            foreach (var word in words)
            {
                var testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                var size = gfx.MeasureString(testLine, font);
                if (size.Width > maxWidth)
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private struct TextSegment
        {
            public string Text { get; set; }
            public XFont Font { get; set; }
            public XBrush Brush { get; set; }
        }

        private void DrawCenteredSegments(XGraphics gfx, double y, params TextSegment[] segments)
        {
            double totalWidth = 0;
            foreach (var seg in segments)
            {
                totalWidth += gfx.MeasureString(seg.Text, seg.Font).Width;
            }

            double currentX = (595 - totalWidth) / 2.0;
            foreach (var seg in segments)
            {
                gfx.DrawString(seg.Text, seg.Font, seg.Brush, currentX, y);
                currentX += gfx.MeasureString(seg.Text, seg.Font).Width;
            }
        }
    }
}
