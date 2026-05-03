using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resume.Api.Data;
using Resume.Api.DTOs;
using Resume.Api.Extensions;

namespace Resume.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ResultsController(AppDbContext db) : ControllerBase
{
    [HttpGet("{jobId:int}")]
    public async Task<IActionResult> GetResults(int jobId)
    {
        var userId = User.GetUserId();

        // Ownership check: the job must belong to the authenticated user.
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);
        if (job is null) return NotFound();

        var scores = await db.CandidateScores
            .Include(s => s.Candidate)
            .Where(s => s.JobId == jobId)
            .OrderBy(s => s.Rank)
            .ToListAsync();

        var feedbackByCandidate = await db.Feedbacks
            .Where(f => f.JobId == jobId)
            .ToDictionaryAsync(f => f.CandidateId, f => f.Type.ToString());

        var results = scores.Select(s => new CandidateResultResponse(
            s.CandidateId,
            s.Id,
            s.Candidate.Name,
            s.Candidate.Email,
            s.Candidate.FileName,
            s.Score,
            s.Rank,
            SplitCsv(s.CoreMatchedKeywords),
            SplitCsv(s.CoreMissingKeywords),
            SplitCsv(s.SecondaryMatchedKeywords),
            SplitCsv(s.SecondaryMissingKeywords),
            SplitCsv(s.HardFilters),
            SplitCsv(s.ScoreReasons),
            s.TotalCoreKeywords,
            s.TotalSecondaryKeywords,
            feedbackByCandidate.GetValueOrDefault(s.CandidateId)
        )).ToList();

        return Ok(new
        {
            jobId,
            jobTitle = job.Title,
            jobDescription = job.Description,
            totalCandidates = results.Count,
            averageScore = results.Count > 0 ? (int)results.Average(r => r.Score) : 0,
            results
        });
    }

    private static List<string> SplitCsv(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
