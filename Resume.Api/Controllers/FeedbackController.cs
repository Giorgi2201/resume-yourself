using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resume.Api.Data;
using Resume.Api.DTOs;
using Resume.Api.Exceptions;
using Resume.Api.Extensions;
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
        var userId = User.GetUserId();

        if (!Enum.TryParse<FeedbackType>(request.Type, true, out var feedbackType))
            throw new ApiException("Invalid feedback type. Use 'Approved' or 'Rejected'.");

        // Ownership check: job must belong to this user.
        var jobExists = await db.Jobs.AnyAsync(j => j.Id == request.JobId && j.UserId == userId);
        if (!jobExists)
            throw new ApiException("Job not found.", StatusCodes.Status404NotFound);

        var existing = await db.Feedbacks
            .FirstOrDefaultAsync(f => f.CandidateId == request.CandidateId && f.JobId == request.JobId);

        if (existing is not null)
        {
            existing.Type = feedbackType;
            existing.CreatedAt = DateTimeOffset.UtcNow;
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
        await auditService.LogAsync("feedback.saved", "job", request.JobId.ToString(),
            $"candidateId={request.CandidateId}, type={feedbackType}");

        return Ok(new { message = "Feedback saved.", type = feedbackType.ToString() });
    }

    [HttpDelete]
    public async Task<IActionResult> Remove([FromQuery] int candidateId, [FromQuery] int jobId)
    {
        var userId = User.GetUserId();

        // Ownership check: job must belong to this user.
        var jobExists = await db.Jobs.AnyAsync(j => j.Id == jobId && j.UserId == userId);
        if (!jobExists) return NotFound();

        var feedback = await db.Feedbacks
            .FirstOrDefaultAsync(f => f.CandidateId == candidateId && f.JobId == jobId);

        if (feedback is null) return NotFound();

        db.Feedbacks.Remove(feedback);
        await db.SaveChangesAsync();
        await auditService.LogAsync("feedback.removed", "job", jobId.ToString(),
            $"candidateId={candidateId}");

        return NoContent();
    }
}
