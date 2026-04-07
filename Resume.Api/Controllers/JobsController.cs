using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resume.Api.Data;
using Resume.Api.DTOs;
using Resume.Api.Models;
using Resume.Api.Services;

namespace Resume.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JobsController(AppDbContext db, IJobCandidateUploadService uploadService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var jobs = await db.Jobs
            .OrderByDescending(j => j.CreatedAt)
            .Select(j => new JobResponse(j.Id, j.Title, j.Description, j.CreatedAt))
            .ToListAsync();
        return Ok(jobs);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var jobs = await db.Jobs
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();

        var result = new List<object>();
        foreach (var job in jobs)
        {
            var scores = await db.CandidateScores
                .Where(s => s.JobId == job.Id)
                .ToListAsync();

            var feedbacks = await db.Feedbacks
                .Where(f => f.JobId == job.Id)
                .ToListAsync();

            result.Add(new
            {
                job.Id,
                job.Title,
                job.CreatedAt,
                TotalCandidates = scores.Count,
                AverageScore = scores.Count > 0 ? (int)scores.Average(s => s.Score) : 0,
                TopScore = scores.Count > 0 ? scores.Max(s => s.Score) : 0,
                ApprovedCount = feedbacks.Count(f => f.Type == FeedbackType.Approved),
                RejectedCount = feedbacks.Count(f => f.Type == FeedbackType.Rejected),
            });
        }

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var job = await db.Jobs.FindAsync(id);
        if (job is null) return NotFound();
        return Ok(new JobResponse(job.Id, job.Title, job.Description, job.CreatedAt));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateJobRequest request)
    {
        var job = new Job
        {
            Title = request.Title,
            Description = request.Description
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = job.Id },
            new JobResponse(job.Id, job.Title, job.Description, job.CreatedAt));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var job = await db.Jobs.FindAsync(id);
        if (job is null) return NotFound();
        db.Jobs.Remove(job);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{jobId:int}/candidates/upload")]
    public async Task<IActionResult> UploadCandidates(int jobId, [FromForm] List<IFormFile> files)
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
