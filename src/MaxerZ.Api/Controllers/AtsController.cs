using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MaxerZ.Api.Models;
using MaxerZ.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace MaxerZ.Api.Controllers
{
    [ApiController]
    [Route("api/ats")]
    public class AtsController : ControllerBase
    {
        private readonly AtsService _atsService;
        private readonly ILogger<AtsController> _logger;

        public AtsController(AtsService atsService, ILogger<AtsController> logger)
        {
            _atsService = atsService;
            _logger = logger;
        }

        [HttpPost("analyze")]
        public async Task<IActionResult> Analyze([FromBody] AtsRequest req, CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.JobTitle))
            {
                return BadRequest(new { error = "Target job title is required before ATS scoring can begin. Please specify a job title." });
            }

            if (string.IsNullOrWhiteSpace(req.ResumeText))
            {
                return BadRequest(new { error = "Resume text or document content is required for ATS scoring." });
            }

            try
            {
                var result = await _atsService.AnalyzeAsync(req, ct);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to analyze resume for ATS scoring.");
                return StatusCode(500, new { error = "An error occurred during ATS analysis: " + ex.Message });
            }
        }
    }
}
