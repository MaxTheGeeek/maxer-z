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
    public class CoverLetterController : ControllerBase
    {
        private readonly LlmOrchestrator _llm;
        private readonly PdfService _pdf;
        private readonly McpService _mcp;
        private readonly AppDbContext _db;

        public CoverLetterController(
            LlmOrchestrator llm,
            PdfService pdf,
            McpService mcp,
            AppDbContext db)
        {
            _llm = llm;
            _pdf = pdf;
            _mcp = mcp;
            _db = db;
        }

        // POST /api/coverletter/preview
        [HttpPost("preview")]
        public async Task<IActionResult> Preview(
            [FromBody] CoverLetterRequest req, CancellationToken ct)
        {
            if (req == null) return BadRequest("Request body cannot be null.");

            var layout = await _llm.ValidateAndLayoutAsync(req, ct);
            var pdfBytes = _pdf.GeneratePdf(req, layout);
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

        // POST /api/coverletter/export
        [HttpPost("export")]
        public async Task<IActionResult> Export(
            [FromBody] CoverLetterRequest req, CancellationToken ct)
        {
            if (req == null) return BadRequest("Request body cannot be null.");

            var layout = await _llm.ValidateAndLayoutAsync(req, ct);
            var pdfBytes = _pdf.GeneratePdf(req, layout);
            var pdfPath = _pdf.SavePdf(pdfBytes, req.CompanyName);

            var record = new CoverLetterRecord
            {
                CompanyName = req.CompanyName,
                ContactPerson = req.ContactPerson,
                Department = req.Department,
                CompanyLocation = req.CompanyLocation,
                Position = req.Position,
                Language = req.Language,
                ContentBody = req.CoverLetterBody,
                PdfPath = pdfPath,
                UsedProvider = layout.UsedProvider,
                UsedModel = layout.UsedModel,
                Status = "exported"
            };

            _db.CoverLetters.Add(record);
            await _db.SaveChangesAsync(ct);

            var synced = await _mcp.SaveCoverLetterAsync(record);
            if (synced)
            {
                record.SyncedToMcp = true;
                await _db.SaveChangesAsync(ct);
            }

            return Ok(new
            {
                pdfPath,
                recordId = record.Id,
                syncedToMcp = synced,
                usedProvider = layout.UsedProvider,
                usedModel = layout.UsedModel,
                attemptLog = layout.AttemptLog
            });
        }

        // GET /api/coverletter/history
        [HttpGet("history")]
        public async Task<IActionResult> History()
        {
            var records = await _db.CoverLetters
                .OrderByDescending(r => r.CreatedAt)
                .Take(50)
                .ToListAsync();
            return Ok(records);
        }
    }
}
