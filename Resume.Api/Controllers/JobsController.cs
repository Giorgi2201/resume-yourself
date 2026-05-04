using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
public class JobsController(AppDbContext db, IJobCandidateUploadService uploadService, IAuditService auditService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = User.GetUserId();

        var jobs = await db.Jobs
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobResponse(j.Id, j.Title, j.Description, j.CreatedAt))
            .ToListAsync();

        return Ok(jobs);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var userId = User.GetUserId();

        var jobs = await db.Jobs
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new { j.Id, j.Title, j.CreatedAt })
            .ToListAsync();

        var jobIds = jobs.Select(j => j.Id).ToList();

        // Avoid joining grouped IQueryable subqueries (poorly supported on SQLite).
        var scoreRows = await db.CandidateScores
            .Where(s => jobIds.Contains(s.JobId))
            .GroupBy(s => s.JobId)
            .Select(g => new
            {
                JobId = g.Key,
                TotalCandidates = g.Count(),
                AverageScore = g.Average(x => x.Score),
                TopScore = g.Max(x => x.Score)
            })
            .ToListAsync();

        var feedbackRows = await db.Feedbacks
            .Where(f => jobIds.Contains(f.JobId))
            .GroupBy(f => f.JobId)
            .Select(g => new
            {
                JobId = g.Key,
                ApprovedCount = g.Count(x => x.Type == FeedbackType.Approved),
                RejectedCount = g.Count(x => x.Type == FeedbackType.Rejected)
            })
            .ToListAsync();

        var scoresByJob = scoreRows.ToDictionary(x => x.JobId);
        var feedbackByJob = feedbackRows.ToDictionary(x => x.JobId);

        var summary = jobs.Select(j =>
        {
            scoresByJob.TryGetValue(j.Id, out var s);
            feedbackByJob.TryGetValue(j.Id, out var f);
            return new JobSummaryResponse(
                j.Id,
                j.Title,
                j.CreatedAt,
                s?.TotalCandidates ?? 0,
                s != null ? (int)Math.Round(s.AverageScore) : 0,
                s?.TopScore ?? 0,
                f?.ApprovedCount ?? 0,
                f?.RejectedCount ?? 0
            );
        }).ToList();

        return Ok(summary);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var userId = User.GetUserId();

        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);
        if (job is null) return NotFound();

        return Ok(new JobResponse(job.Id, job.Title, job.Description, job.CreatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobRequest request)
    {
        var userId = User.GetUserId();

        if (string.IsNullOrWhiteSpace(request.Description) || request.Description.Length < 20)
            throw new ApiException("Job description must be at least 20 characters.");

        var job = new Job
        {
            UserId = userId,
            Title = request.Title,
            Description = request.Description
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        await auditService.LogAsync("job.created", "job", job.Id.ToString(), $"title={job.Title}");

        return CreatedAtAction(nameof(GetById), new { id = job.Id },
            new JobResponse(job.Id, job.Title, job.Description, job.CreatedAt));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.GetUserId();

        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == id && j.UserId == userId);
        if (job is null) return NotFound();

        db.Jobs.Remove(job);
        await db.SaveChangesAsync();
        await auditService.LogAsync("job.deleted", "job", job.Id.ToString(), $"title={job.Title}");

        return NoContent();
    }

    [HttpPost("{jobId:int}/candidates/upload")]
    [EnableRateLimiting("upload")]
    public async Task<IActionResult> UploadCandidates(int jobId, [FromForm] List<IFormFile> files)
    {
        var userId = User.GetUserId();

        var response = await uploadService.UploadAsync(jobId, userId, files);
        await auditService.LogAsync("candidates.uploaded", "job", jobId.ToString(),
            $"processed={response.ProcessedCount}, failed={response.FailedCount}");

        return Ok(response);
    }
}
