using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resume.Api.Extensions;
using Resume.Api.Services;

namespace Resume.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CandidatesController(IJobCandidateUploadService uploadService, IAuditService auditService) : ControllerBase
{
    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromForm] int jobId, [FromForm] List<IFormFile> files)
    {
        var userId = User.GetUserId();

        var response = await uploadService.UploadAsync(jobId, userId, files);
        await auditService.LogAsync("candidates.uploaded", "job", jobId.ToString(),
            $"processed={response.ProcessedCount}, failed={response.FailedCount}");

        return Ok(response);
    }
}
