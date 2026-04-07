using Microsoft.AspNetCore.Mvc;
using Resume.Api.Services;

namespace Resume.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CandidatesController(IJobCandidateUploadService uploadService) : ControllerBase
{
    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] int jobId, [FromForm] List<IFormFile> files)
    {
        try
        {
            var response = await uploadService.UploadAsync(jobId, files);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("Job not found", StringComparison.OrdinalIgnoreCase))
                return NotFound(new { message = ex.Message });
            return BadRequest(new { message = ex.Message });
        }
    }
}
