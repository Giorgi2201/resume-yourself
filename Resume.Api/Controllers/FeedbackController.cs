using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resume.Api.Data;
using Resume.Api.DTOs;
using Resume.Api.Models;

namespace Resume.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController(AppDbContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] FeedbackRequest request)
    {
        if (!Enum.TryParse<FeedbackType>(request.Type, true, out var feedbackType))
            return BadRequest(new { message = "Invalid feedback type. Use 'Approved' or 'Rejected'." });

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
        return NoContent();
    }
}
