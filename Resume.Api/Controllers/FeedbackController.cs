using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Resume.Api.Data;
using Resume.Api.DTOs;
using Resume.Api.Exceptions;
using Resume.Api.Models;
using Resume.Api.Services;

namespace Resume.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FeedbackController(AppDbContext db, IAuditService auditService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] FeedbackRequest request)
    {
        if (!Enum.TryParse<FeedbackType>(request.Type, true, out var feedbackType))
            throw new ApiException("Invalid feedback type. Use 'Approved' or 'Rejected'.");

        var existing = await db.Feedbacks
            .FirstOrDefaultAsync(f => f.CandidateId == request.CandidateId && f.JobId == request.JobId);

        if (existing is not null)
        {
            existing.Type = feedbackType;
            existing.CreatedAt = DateTime.UtcNow;
        }
        else
        {
            db.Feedbacks.Add(new Feedback
            {
                CandidateId = request.CandidateId,
                JobId = request.JobId,
                Type = feedbackType
            });
        }

        await db.SaveChangesAsync();
        await auditService.LogAsync("feedback.saved", "job", request.JobId.ToString(), $"candidateId={request.CandidateId}, type={feedbackType}");
        return Ok(new { message = "Feedback saved.", type = feedbackType.ToString() });
    }

    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int candidateId, [FromQuery] int jobId)
    {
        var feedback = await db.Feedbacks
            .FirstOrDefaultAsync(f => f.CandidateId == candidateId && f.JobId == jobId);
        if (feedback is null) return NotFound();

        db.Feedbacks.Remove(feedback);
        await db.SaveChangesAsync();
        await auditService.LogAsync("feedback.removed", "job", jobId.ToString(), $"candidateId={candidateId}");
        return NoContent();
    }
}
