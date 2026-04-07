using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Resume.Api.Data;
using Resume.Api.DTOs;
using Resume.Api.Models;

namespace Resume.Api.Services;

public class JobCandidateUploadService(
    AppDbContext db,
    ICvParserService parser,
    IScoringService scorer) : IJobCandidateUploadService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".docx", ".txt"
    };

    public async Task<CandidateUploadResponse> UploadAsync(int jobId, List<IFormFile> files)
    {
        var job = await db.Jobs.FindAsync(jobId);
        if (job is null) throw new InvalidOperationException("Job not found.");
        if (files is null || files.Count == 0) throw new InvalidOperationException("No files uploaded.");

        var results = new List<CandidateUploadFileResult>();
        var createdScoreIds = new List<int>();

        // Build existing hash set for duplicate protection within this job.
        var existingHashes = await db.CandidateScores
            .Where(s => s.JobId == jobId)
            .Include(s => s.Candidate)
            .Select(s => s.Candidate.ParsedText)
            .ToListAsync();
        var hashSet = existingHashes.Select(ComputeHash).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                results.Add(new CandidateUploadFileResult(
                    FileName: file.FileName,
                    Status: "failed",
                    Message: "Unsupported file type. Please upload PDF, DOCX, or TXT.",
                    CandidateId: null,
                    Score: null,
                    Rank: null
                ));
                continue;
            }

            try
            {
                var parsed = await parser.ParseAsync(file);
                if (string.IsNullOrWhiteSpace(parsed.RawText))
                {
                    results.Add(new CandidateUploadFileResult(
                        file.FileName, "failed",
                        "Could not extract readable text from this CV.",
                        null, null, null));
                    continue;
                }

                var hash = ComputeHash(parsed.RawText);
                if (hashSet.Contains(hash))
                {
                    results.Add(new CandidateUploadFileResult(
                        file.FileName, "failed",
                        "Duplicate CV detected for this job.",
                        null, null, null));
                    continue;
                }

                var candidate = new Candidate
                {
                    FileName = file.FileName,
                    ParsedText = parsed.RawText,
                    Name = parsed.Name,
                    Email = parsed.Email,
                    ExtractedSkills = string.Join(", ", parsed.ExtractedSkills),
                    UploadedAt = DateTime.UtcNow
                };
                db.Candidates.Add(candidate);
                await db.SaveChangesAsync();

                var scored = scorer.Score(parsed.RawText, job.Description);
                var score = new CandidateScore
                {
                    CandidateId = candidate.Id,
                    JobId = jobId,
                    Score = scored.Score,
                    Rank = 0,
                    MatchedKeywords = string.Join(",", scored.AllMatched),
                    MissingKeywords = string.Join(",", scored.AllMissing),
                    TotalJobKeywords = scored.TotalKeywords,
                    ScoredAt = DateTime.UtcNow
                };
                db.CandidateScores.Add(score);
                await db.SaveChangesAsync();

                createdScoreIds.Add(score.Id);
                hashSet.Add(hash);

                results.Add(new CandidateUploadFileResult(
                    FileName: file.FileName,
                    Status: "completed",
                    Message: "Processed successfully.",
                    CandidateId: candidate.Id,
                    Score: score.Score,
                    Rank: null
                ));
            }
            catch
            {
                results.Add(new CandidateUploadFileResult(
                    FileName: file.FileName,
                    Status: "failed",
                    Message: "Failed to parse this file.",
                    CandidateId: null,
                    Score: null,
                    Rank: null
                ));
            }
        }

        await RecomputeRanksAsync(jobId);

        if (createdScoreIds.Count > 0)
        {
            var createdScores = await db.CandidateScores
                .Where(s => createdScoreIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.CandidateId, s => s.Rank);

            results = results.Select(r =>
            {
                if (r.CandidateId is null) return r;
                return createdScores.TryGetValue(r.CandidateId.Value, out var rank)
                    ? r with { Rank = rank }
                    : r;
            }).ToList();
        }

        return new CandidateUploadResponse(
            JobId: jobId,
            ProcessedCount: results.Count(r => r.Status == "completed"),
            FailedCount: results.Count(r => r.Status == "failed"),
            Files: results
        );
    }

    private async Task RecomputeRanksAsync(int jobId)
    {
        var scores = await db.CandidateScores
            .Where(s => s.JobId == jobId)
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.ScoredAt)
            .ToListAsync();

        for (int i = 0; i < scores.Count; i++)
            scores[i].Rank = i + 1;

        await db.SaveChangesAsync();
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim()));
        return Convert.ToHexString(bytes);
    }
}

