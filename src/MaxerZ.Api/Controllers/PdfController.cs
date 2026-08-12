using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MaxerZ.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MaxerZ.Api.Controllers
{
    [ApiController]
    [Route("api/pdf")]
    public class PdfController : ControllerBase
    {
        private readonly PdfService _pdfService;
        private readonly ILogger<PdfController> _logger;

        public PdfController(PdfService pdfService, ILogger<PdfController> logger)
        {
            _pdfService = pdfService;
            _logger = logger;
        }

        [HttpPost("merge")]
        public async Task<IActionResult> MergePdfs([FromForm] List<IFormFile> files, CancellationToken ct)
        {
            if (files == null || files.Count == 0)
            {
                return BadRequest(new { error = "Please select at least 1 PDF file to merge." });
            }

            if (files.Count > 5)
            {
                return BadRequest(new { error = "You can merge a maximum of 5 PDF files at a time." });
            }

            var pdfBytesList = new List<byte[]>();
            foreach (var file in files)
            {
                if (file == null || file.Length == 0) continue;
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                pdfBytesList.Add(ms.ToArray());
            }

            if (pdfBytesList.Count == 0)
            {
                return BadRequest(new { error = "Uploaded files were empty." });
            }

            try
            {
                var (mergedBytes, pageCount) = _pdfService.MergePdfs(pdfBytesList);
                var base64 = Convert.ToBase64String(mergedBytes);

                return Ok(new
                {
                    pdfBase64 = base64,
                    pageCount = pageCount,
                    fileName = "merged_document.pdf"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to merge PDF files.");
                return StatusCode(500, new { error = "Failed to merge PDF files. Ensure all uploaded files are valid, unencrypted PDFs." });
            }
        }
    }
}
