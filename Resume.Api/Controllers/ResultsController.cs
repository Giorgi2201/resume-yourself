using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resume.Api.Data;
using Resume.Api.DTOs;
using Resume.Api.Services;

namespace Resume.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ResultsController(AppDbContext db, IScoringService scorer) : ControllerBase
{
    [HttpGet("{jobId:int}")]
    public async Task<IActionResult> GetResults(int jobId)
    {
        var job = await db.Jobs.FindAsync(jobId);
        if (job is null) return NotFound();

        var scores = await db.CandidateScores
            .Include(s => s.Candidate)
            .Where(s => s.JobId == jobId)
            .OrderBy(s => s.Rank)
            .ToListAsync();

        var feedbacks = await db.Feedbacks
            .Where(f => f.JobId == jobId)
            .ToListAsync();

        var results = scores.Select(s =>
        {
            // Re-run scoring to get the full weighted breakdown (core / secondary / hard filters)
            var breakdown = scorer.Score(s.Candidate.ParsedText, job.Description);
            var feedback = feedbacks.FirstOrDefault(f => f.CandidateId == s.CandidateId);

            return new CandidateResultResponse(
                s.CandidateId,
                s.Id,
                s.Candidate.Name,
                s.Candidate.Email,
                s.Candidate.FileName,
                s.Score,
                s.Rank,
                breakdown.CoreMatched,
                breakdown.CoreMissing,
                breakdown.SecondaryMatched,
                breakdown.SecondaryMissing,
                breakdown.HardFilters,
                breakdown.Explanations,
                breakdown.TotalCoreKeywords,
                breakdown.TotalSecondaryKeywords,
                feedback?.Type.ToString()
            );
        }).ToList();

        return Ok(new
        {
            jobId,
            jobTitle = job.Title,
            totalCandidates = results.Count,
            averageScore = results.Count > 0 ? (int)results.Average(r => r.Score) : 0,
            results
        });
    }
}
