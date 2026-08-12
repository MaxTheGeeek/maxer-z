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

            // Load profile settings
            var profile = _settings.Get().Profile;
            var name = string.IsNullOrWhiteSpace(profile.FullName) ? "MAX MUSTERMANN" : profile.FullName.ToUpper();
            var role = string.IsNullOrWhiteSpace(profile.Role) ? "Software Engineer | C# & .NET | TypeScript & Angular" : profile.Role;
            var phone = string.IsNullOrWhiteSpace(profile.Phone) ? "+43 123 4567890" : profile.Phone;
            var email = string.IsNullOrWhiteSpace(profile.Email) ? "max.mustermann@muster.com" : profile.Email;
            var linkedin = string.IsNullOrWhiteSpace(profile.LinkedInUrl) ? "linkedin.com/in/muster" : profile.LinkedInUrl;
            var website = string.IsNullOrWhiteSpace(profile.WebsiteUrl) ? "www.muster.com" : profile.WebsiteUrl;
            var github = string.IsNullOrWhiteSpace(profile.GitHubUrl) ? "github.com/muster" : profile.GitHubUrl;

            // Line 1: Name
            DrawCenteredSegments(gfx, 50, new TextSegment
            {
                Text = name,
                Font = headerNameFont,
                Brush = brushHeaderBlue
            });

            // Line 2: Role / Skills
            DrawCenteredSegments(gfx, 68, new TextSegment
            {
                Text = role,
                Font = headerRoleFont,
                Brush = brushHeaderDark
            });

            // Line 3: Contact Details
            DrawCenteredSegments(gfx, 83, 
                new TextSegment { Text = $"{phone} | {email} | ", Font = headerInfoFont, Brush = brushHeaderDark },
                new TextSegment { Text = linkedin, Font = headerInfoFont, Brush = brushHeaderBlue },
                new TextSegment { Text = " | Portfolio: ", Font = headerInfoFont, Brush = brushHeaderDark },
                new TextSegment { Text = website, Font = headerInfoFont, Brush = brushHeaderBlue }
            );

            // Line 4: GitHub & Address
            var addressText = string.IsNullOrWhiteSpace(request.HeaderAddress) 
                ? (profile.Address ?? "Musterstraße 1, 1010 Wien")
                : request.HeaderAddress;

            DrawCenteredSegments(gfx, 98, 
                new TextSegment { Text = github, Font = headerInfoFont, Brush = brushHeaderBlue },
                new TextSegment { Text = " | " + addressText, Font = headerInfoFont, Brush = brushHeaderDark }
            );

            // Draw a blue divider line under the header
            var penAccent = new XPen(XColor.FromArgb(0x00, 0x6C, 0xA5), 1.5);
            gfx.DrawLine(penAccent, 42, 112, 553, 112);

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
            var companyName = !string.IsNullOrWhiteSpace(layout.CompanyNameFormatted) 
                ? layout.CompanyNameFormatted 
                : (!string.IsNullOrWhiteSpace(request.CompanyName) ? request.CompanyName : "");
            gfx.DrawString(companyName, fontCompany, brushPrimary, 42, currentY);
            currentY += 14;

            // 2. Contact person (regular, secondary color)
            var contactPerson = !string.IsNullOrWhiteSpace(request.ContactPerson) 
                ? request.ContactPerson 
                : (!string.IsNullOrWhiteSpace(layout.ContactPerson) ? layout.ContactPerson : "");
            if (!string.IsNullOrWhiteSpace(contactPerson))
            {
                gfx.DrawString(contactPerson, fontInfo, brushSecondary, 42, currentY);
                currentY += 12;
            }

            // 3. Department (regular, secondary color)
            var department = !string.IsNullOrWhiteSpace(request.Department) 
                ? request.Department 
                : (!string.IsNullOrWhiteSpace(layout.Department) ? layout.Department : "");
            if (!string.IsNullOrWhiteSpace(department))
            {
                gfx.DrawString(department, fontInfo, brushSecondary, 42, currentY);
                currentY += 12;
            }

            // 4. Company Location (regular, secondary color)
            var companyLocation = !string.IsNullOrWhiteSpace(request.CompanyLocation) 
                ? request.CompanyLocation 
                : (!string.IsNullOrWhiteSpace(layout.CompanyLocation) ? layout.CompanyLocation : "");
            if (!string.IsNullOrWhiteSpace(companyLocation))
            {
                gfx.DrawString(companyLocation, fontInfo, brushSecondary, 42, currentY);
                currentY += 12;
            }

            // 5. Date (drawn on the same line as Company Name, aligned right)
            var dateSize = gfx.MeasureString(today ?? "", fontInfo);
            gfx.DrawString(today ?? "", fontInfo, brushPrimary, 553 - dateSize.Width, 130);

            // 6. Subject Line / Position (bold, accent color)
            currentY += 23; // Spacing before subject line
            var posText = !string.IsNullOrWhiteSpace(layout.PositionFormatted)
                ? layout.PositionFormatted
                : (!string.IsNullOrWhiteSpace(request.Position) ? request.Position : "");
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
                gfx.DrawString(line ?? "", fontSubject, brushAccent, 42, currentY);
                currentY += 14;
            }

            // 7. Salutation
            currentY += 20;
            gfx.DrawString(layout.SalutationLine ?? "", fontBody, brushPrimary, 42, currentY);
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
                    gfx.DrawString(line ?? "", fontBody, brushPrimary, 42, currentY);
                    currentY += 15; // Font size 10.5 with line-height
                }
                currentY += 10; // Paragraph spacing
            }

            // 9. Closing Line
            currentY += 10;
            if (currentY <= 740)
            {
                gfx.DrawString(layout.ClosingLine ?? "", fontBody, brushPrimary, 42, currentY);
                currentY += 30;
            }

            // 10. Signer Name
            if (currentY <= 740)
            {
                gfx.DrawString(layout.SignerName ?? "", fontSigner, brushPrimary, 42, currentY);
            }

            // 11. Custom Footer links
            if (!string.IsNullOrWhiteSpace(profile.FooterText))
            {
                // Wipe bottom footer text zone with a small white rectangle to preserve template margins
                gfx.DrawRectangle(XBrushes.White, 0, 790, 595, 25);

                var footerFont = new XFont("Arial", 8, XFontStyle.Regular);
                var brushFooter = new XSolidBrush(XColor.FromArgb(0x77, 0x77, 0x77));
                DrawCenteredSegments(gfx, 805, new TextSegment
                {
                    Text = profile.FooterText,
                    Font = footerFont,
                    Brush = brushFooter
                });
            }

            // Save PDF to memory stream and return as byte array
            using var ms = new MemoryStream();
            document.Save(ms);
            return ms.ToArray();
        }

        public byte[] GenerateResumePdf(ResumeRequest request, ResumeResult layout)
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
            var gfx = XGraphics.FromPdfPage(page);

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

            // Load profile settings
            var profile = _settings.Get().Profile;
            var name = !string.IsNullOrWhiteSpace(request.FullName)
                ? request.FullName.ToUpper()
                : (!string.IsNullOrWhiteSpace(profile.FullName) ? profile.FullName.ToUpper() : "MAX MUSTERMANN");
            var role = !string.IsNullOrWhiteSpace(request.TargetRole)
                ? request.TargetRole
                : (!string.IsNullOrWhiteSpace(profile.Role) ? profile.Role : "Software Engineer | C# & .NET | TypeScript & Angular");
            var phone = string.IsNullOrWhiteSpace(profile.Phone) ? "+43 123 4567890" : profile.Phone;
            var email = string.IsNullOrWhiteSpace(profile.Email) ? "max.mustermann@muster.com" : profile.Email;
            var linkedin = string.IsNullOrWhiteSpace(profile.LinkedInUrl) ? "linkedin.com/in/muster" : profile.LinkedInUrl;
            var website = string.IsNullOrWhiteSpace(profile.WebsiteUrl) ? "www.muster.com" : profile.WebsiteUrl;
            var github = string.IsNullOrWhiteSpace(profile.GitHubUrl) ? "github.com/muster" : profile.GitHubUrl;

            // Line 1: Name
            DrawCenteredSegments(gfx, 50, new TextSegment
            {
                Text = name,
                Font = headerNameFont,
                Brush = brushHeaderBlue
            });

            // Line 2: Role / Skills
            DrawCenteredSegments(gfx, 68, new TextSegment
            {
                Text = role,
                Font = headerRoleFont,
                Brush = brushHeaderDark
            });

            // Line 3: Contact Details
            DrawCenteredSegments(gfx, 83, 
                new TextSegment { Text = $"{phone} | {email} | ", Font = headerInfoFont, Brush = brushHeaderDark },
                new TextSegment { Text = linkedin, Font = headerInfoFont, Brush = brushHeaderBlue },
                new TextSegment { Text = " | Portfolio: ", Font = headerInfoFont, Brush = brushHeaderDark },
                new TextSegment { Text = website, Font = headerInfoFont, Brush = brushHeaderBlue }
            );

            // Line 4: GitHub & Address
            var addressText = string.IsNullOrWhiteSpace(request.HeaderAddress) 
                ? (profile.Address ?? "Musterstraße 1, 1010 Wien")
                : request.HeaderAddress;

            DrawCenteredSegments(gfx, 98, 
                new TextSegment { Text = github, Font = headerInfoFont, Brush = brushHeaderBlue },
                new TextSegment { Text = " | " + addressText, Font = headerInfoFont, Brush = brushHeaderDark }
            );

            // Draw a blue divider line under the header
            var penAccent = new XPen(XColor.FromArgb(0x00, 0x6C, 0xA5), 1.5);
            gfx.DrawLine(penAccent, 42, 112, 553, 112);

            // Content drawing setup
            double currentY = 135;
            
            var fontSectionTitle = new XFont("Arial", 11, XFontStyle.Bold);
            var fontBody = new XFont("Arial", 9.5, XFontStyle.Regular);
            var fontBodyBold = new XFont("Arial", 9.5, XFontStyle.Bold);

            // Helper to draw sections
            void DrawSection(string title, string content)
            {
                if (string.IsNullOrWhiteSpace(content)) return;

                // Check page transition before drawing title
                if (currentY > 720)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    currentY = 50;
                }

                // Draw section title
                gfx.DrawString(title.ToUpper(), fontSectionTitle, brushAccent, 42, currentY);
                currentY += 14;

                // Draw thin underline for section title
                var penDivider = new XPen(XColor.FromArgb(0xEE, 0xEE, 0xEE), 1);
                gfx.DrawLine(penDivider, 42, currentY - 11, 553, currentY - 11);
                currentY += 4;

                // Process lines/paragraphs of content
                var lines = content.Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => l.Length > 0 || l == "")
                    .ToList();

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        currentY += 6; // Paragraph spacing
                        continue;
                    }

                    // Handle list item bullet formatting
                    bool isBullet = line.StartsWith("-") || line.StartsWith("*") || line.StartsWith("•");
                    string textToDraw = line;
                    double leftIndent = 42;

                    if (isBullet)
                    {
                        textToDraw = line.Substring(1).Trim();
                        // Draw bullet point dot/circle
                        gfx.DrawString("•", fontBodyBold, brushPrimary, 42, currentY);
                        leftIndent = 52;
                    }

                    var wrappedLines = WrapText(textToDraw, 553 - leftIndent, gfx, fontBody);
                    foreach (var wl in wrappedLines)
                    {
                        if (currentY > 740)
                        {
                            page = document.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            currentY = 50;
                            if (isBullet) leftIndent = 52;
                        }
                        gfx.DrawString(wl ?? "", fontBody, brushPrimary, leftIndent, currentY);
                        currentY += 13.5;
                    }
                }

                currentY += 15; // Spacing after section
            }

            void DrawLanguagesSection(string title, List<LanguageItem>? languages)
            {
                if (languages == null || languages.Count == 0) return;
                var validLangs = languages.Where(l => !string.IsNullOrWhiteSpace(l.Language)).ToList();
                if (validLangs.Count == 0) return;

                if (currentY > 720)
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    currentY = 50;
                }

                gfx.DrawString(title.ToUpper(), fontSectionTitle, brushAccent, 42, currentY);
                currentY += 14;

                var penDivider = new XPen(XColor.FromArgb(0xEE, 0xEE, 0xEE), 1);
                gfx.DrawLine(penDivider, 42, currentY - 11, 553, currentY - 11);
                currentY += 4;

                foreach (var lang in validLangs)
                {
                    if (currentY > 740)
                    {
                        page = document.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        currentY = 50;
                    }

                    gfx.DrawString("•", fontBodyBold, brushPrimary, 42, currentY);
                    gfx.DrawString(lang.Language, fontBodyBold, brushPrimary, 52, currentY);

                    if (!string.IsNullOrWhiteSpace(lang.Proficiency))
                    {
                        var langWidth = gfx.MeasureString(lang.Language, fontBodyBold).Width;
                        gfx.DrawString($" - {lang.Proficiency}", fontBody, brushSecondary, 52 + langWidth, currentY);
                    }
                    currentY += 13.5;
                }

                currentY += 15;
            }

            // Draw resume sections in dynamic order requested by user
            var isDe = (request.Language ?? "en").ToLower() == "de";
            var defaultOrder = new List<string> { "summary", "experience", "education", "skills", "projects", "languages" };
            var sectionOrder = (request.SectionOrder != null && request.SectionOrder.Count > 0)
                ? request.SectionOrder
                : defaultOrder;

            foreach (var secKey in sectionOrder)
            {
                switch (secKey.ToLower())
                {
                    case "summary":
                        DrawSection(isDe ? "Zusammenfassung" : "Professional Summary", layout.SummaryFormatted);
                        break;
                    case "experience":
                        DrawSection(isDe ? "Berufserfahrung" : "Work Experience", layout.ExperienceFormatted);
                        break;
                    case "education":
                        DrawSection(isDe ? "Ausbildung & Zertifikate" : "Education & Certificates", layout.EducationFormatted);
                        break;
                    case "skills":
                        DrawSection(isDe ? "Kenntnisse" : "Key Skills & Competencies", layout.SkillsFormatted);
                        break;
                    case "projects":
                        DrawSection(isDe ? "Projekte & Erfolge" : "Projects & Key Accomplishments", layout.ProjectsFormatted);
                        break;
                    case "languages":
                        DrawLanguagesSection(isDe ? "Sprachkenntnisse" : "Languages & Proficiency", request.Languages);
                        break;
                }
            }

            // Dispose current graphics context before rendering page footers
            gfx?.Dispose();

            // Custom Footer links on EVERY page
            var footerTextToDraw = !string.IsNullOrWhiteSpace(profile.FooterText)
                ? profile.FooterText
                : $"{phone} | {email}";

            for (int p = 0; p < document.PageCount; p++)
            {
                var docPage = document.Pages[p];
                using var pageGfx = XGraphics.FromPdfPage(docPage);

                pageGfx.DrawRectangle(XBrushes.White, 0, 790, 595, 25);
                var footerFont = new XFont("Arial", 8, XFontStyle.Regular);
                var brushFooter = new XSolidBrush(XColor.FromArgb(0x77, 0x77, 0x77));
                DrawCenteredSegments(pageGfx, 805, new TextSegment
                {
                    Text = footerTextToDraw,
                    Font = footerFont,
                    Brush = brushFooter
                });
            }

            using var ms = new MemoryStream();
            document.Save(ms);
            return ms.ToArray();
        }

        public async Task<string?> SaveResumePdfAsync(byte[] bytes, string signerName)
        {
            var safe = string.Concat(signerName.Split(Path.GetInvalidFileNameChars()));
            var name = $"Resume_{safe}_{DateTime.Now:yyyyMMdd}.pdf";

            if (FileSaveDialogHelper.SaveFileDialogAsync != null)
            {
                var chosenPath = await FileSaveDialogHelper.SaveFileDialogAsync(name, bytes);
                return chosenPath; // returns null if cancelled
            }

            var cfg = _settings.Get();
            var dir = cfg.ExportDirectory
                .Replace("~", Environment.GetFolderPath(
                    Environment.SpecialFolder.Personal));
            var expandedDir = Environment.ExpandEnvironmentVariables(dir);
            Directory.CreateDirectory(expandedDir);

            var path = Path.Combine(expandedDir, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public async Task<string?> SavePdfAsync(byte[] bytes, string companyName)
        {
            var safe = string.Concat(companyName.Split(Path.GetInvalidFileNameChars()));
            var name = $"CoverLetter_{safe}_{DateTime.Now:yyyyMMdd}.pdf";

            if (FileSaveDialogHelper.SaveFileDialogAsync != null)
            {
                var chosenPath = await FileSaveDialogHelper.SaveFileDialogAsync(name, bytes);
                return chosenPath; // returns null if cancelled
            }

            var cfg = _settings.Get();
            var dir = cfg.ExportDirectory
                .Replace("~", Environment.GetFolderPath(
                    Environment.SpecialFolder.Personal));
            var expandedDir = Environment.ExpandEnvironmentVariables(dir);
            Directory.CreateDirectory(expandedDir);

            var path = Path.Combine(expandedDir, name);
            File.WriteAllBytes(path, bytes);
            return path;
        }

        public (byte[] pdfBytes, int pageCount) MergePdfs(List<byte[]> pdfBytesList)
        {
            if (pdfBytesList == null || pdfBytesList.Count == 0)
            {
                throw new ArgumentException("No PDF documents provided to merge.");
            }

            using var outputDocument = new PdfDocument();
            int totalPages = 0;

            foreach (var pdfBytes in pdfBytesList)
            {
                if (pdfBytes == null || pdfBytes.Length == 0) continue;
                using var stream = new MemoryStream(pdfBytes);
                using var inputDocument = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
                for (int i = 0; i < inputDocument.PageCount; i++)
                {
                    outputDocument.AddPage(inputDocument.Pages[i]);
                    totalPages++;
                }
            }

            using var outStream = new MemoryStream();
            outputDocument.Save(outStream);
            return (outStream.ToArray(), totalPages);
        }

        private string FindTemplatePdfPath(string templateName)
        {
            if (!string.IsNullOrEmpty(templateName))
            {
                var customDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MaxerZ", "Templates");
                var sanitizedName = Path.GetFileName(templateName);
                var customPath = Path.Combine(customDir, sanitizedName);
                if (File.Exists(customPath))
                {
                    return customPath;
                }
            }

            var filename = (templateName == "template_2" || templateName == "resume_template_2") 
                ? "coverletter_template_2.pdf" 
                : "coverletter_template_1.pdf";
            var pathsToTry = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "..", "Resources", "Templates", filename),
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
                totalWidth += gfx.MeasureString(seg.Text ?? "", seg.Font).Width;
            }

            double currentX = (595 - totalWidth) / 2.0;
            foreach (var seg in segments)
            {
                gfx.DrawString(seg.Text ?? "", seg.Font, seg.Brush, currentX, y);
                currentX += gfx.MeasureString(seg.Text ?? "", seg.Font).Width;
            }
        }
    }

    public static class FileSaveDialogHelper
    {
        public static Func<string, byte[], Task<string?>>? SaveFileDialogAsync { get; set; }
    }
}
