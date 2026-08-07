using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MaxerZ.Api.Models;
using MaxerZ.Api.Services;
using MaxerZ.Api.Data;

namespace MaxerZ.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ResumeController : ControllerBase
    {
        private readonly LlmOrchestrator _llm;
        private readonly PdfService _pdf;
        private readonly McpService _mcp;
        private readonly AppDbContext _db;
        private readonly SettingsService _settings;
        public static byte[]? LastPreviewPdf;

        public ResumeController(
            LlmOrchestrator llm,
            PdfService pdf,
            McpService mcp,
            SettingsService settings,
            AppDbContext db)
        {
            _llm = llm;
            _pdf = pdf;
            _mcp = mcp;
            _settings = settings;
            _db = db;
        }

        // POST /api/resume/preview
        [HttpPost("preview")]
        public async Task<IActionResult> Preview(
            [FromBody] ResumeRequest req, CancellationToken ct)
        {
            if (req == null) return BadRequest("Request body cannot be null.");

            var layout = await _llm.ValidateAndLayoutResumeAsync(req, ct);
            var pdfBytes = _pdf.GenerateResumePdf(req, layout);
            LastPreviewPdf = pdfBytes;
            return Ok(new
            {
                pdfBase64 = Convert.ToBase64String(pdfBytes),
                layout,
                attemptLog = layout.AttemptLog,
                warnings = layout.Warnings,
                usedProvider = layout.UsedProvider,
                usedModel = layout.UsedModel,
                wasFallback = layout.WasFallback
            });
        }

        // POST /api/resume/export
        [HttpPost("export")]
        public async Task<IActionResult> Export(
            [FromBody] ResumeRequest req, CancellationToken ct)
        {
            if (req == null) return BadRequest("Request body cannot be null.");

            var layout = await _llm.ValidateAndLayoutResumeAsync(req, ct);
            var pdfBytes = _pdf.GenerateResumePdf(req, layout);
            LastPreviewPdf = pdfBytes;

            var profileName = _settings.Get().Profile.FullName ?? "Candidate";
            var pdfPath = await _pdf.SaveResumePdfAsync(pdfBytes, profileName);
            if (pdfPath == null)
            {
                return BadRequest(new { error = "Export cancelled by user." });
            }

            var record = new ResumeRecord
            {
                Summary = req.Summary,
                Experience = req.Experience,
                Education = req.Education,
                Skills = req.Skills,
                Projects = req.Projects,
                Language = req.Language,
                SelectedTemplate = req.SelectedTemplate,
                HeaderAddress = req.HeaderAddress,
                PdfPath = pdfPath,
                UsedProvider = layout.UsedProvider,
                UsedModel = layout.UsedModel,
                Status = "exported"
            };

            _db.Resumes.Add(record);
            await _db.SaveChangesAsync(ct);

            // Cover letter sync to MCP exists. We could also index resumes similarly if MCP supports it,
            // or just log it/save it locally. Let's keep it simple for resumes or try to index as well if needed.
            // For now, saving to DB is plenty.
            
            return Ok(new
            {
                pdfPath,
                recordId = record.Id,
                usedProvider = layout.UsedProvider,
                usedModel = layout.UsedModel,
                attemptLog = layout.AttemptLog
            });
        }

        // GET /api/resume/history
        [HttpGet("history")]
        public async Task<IActionResult> History()
        {
            var records = await _db.Resumes
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .ToListAsync();
            return Ok(records);
        }

        // GET /api/resume/preview-pdf
        [HttpGet("preview-pdf")]
        public IActionResult GetPreviewPdf()
        {
            if (LastPreviewPdf == null)
            {
                return NotFound("No preview PDF available.");
            }
            return File(LastPreviewPdf, "application/pdf");
        }
    }
}
