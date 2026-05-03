using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Resume.Api.Configuration;
using Resume.Api.Data;
using Resume.Api.DTOs;
using Resume.Api.Exceptions;
using Resume.Api.Models;

namespace Resume.Api.Services;

public class JobCandidateUploadService(
    AppDbContext db,
    ICvParserService parser,
    IScoringService scorer,
    IOptions<FileUploadOptions> uploadOptions) : IJobCandidateUploadService
{

    public async Task<CandidateUploadResponse> UploadAsync(int jobId, string userId, List<IFormFile> files)
    {
        var job = await db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);
        if (job is null) throw new ApiException("Job not found.", StatusCodes.Status404NotFound);
        if (files is null || files.Count == 0) throw new ApiException("No files uploaded.");
        if (files.Count > uploadOptions.Value.MaxFilesPerRequest)
            throw new ApiException($"Too many files. Maximum allowed is {uploadOptions.Value.MaxFilesPerRequest} files.");

        var results = new List<CandidateUploadFileResult>();

        var existingHashes = await db.CandidateScores
            .Where(s => s.JobId == jobId)
            .Include(s => s.Candidate)
            .Select(s => s.Candidate.ParsedText)
            .ToListAsync();
        var hashSet = existingHashes.Select(ComputeHash).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidatesToPersist = new List<Candidate>();
        var stagedScores = new List<(Candidate Candidate, WeightedScoreResult Scored)>();
        var successEntries = new List<(int CandidateId, string FileName, int Score)>();

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!uploadOptions.Value.AllowedExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
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
            if (!uploadOptions.Value.AllowedMimeTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            {
                results.Add(new CandidateUploadFileResult(file.FileName, "failed", $"Unsupported MIME type '{file.ContentType}'.", null, null, null));
                continue;
            }
            if (file.Length <= 0 || file.Length > uploadOptions.Value.MaxFileSizeBytes)
            {
                results.Add(new CandidateUploadFileResult(file.FileName, "failed", $"File size must be between 1 byte and {uploadOptions.Value.MaxFileSizeBytes} bytes.", null, null, null));
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
                var scored = scorer.Score(parsed.RawText, job.Description);
                candidatesToPersist.Add(candidate);
                stagedScores.Add((candidate, scored));
                hashSet.Add(hash);
            }
            catch (Exception ex)
            {
                results.Add(new CandidateUploadFileResult(
                    FileName: file.FileName,
                    Status: "failed",
                    Message: $"Failed to parse this file: {ex.Message}",
                    CandidateId: null,
                    Score: null,
                    Rank: null
                ));
            }
        }

        if (candidatesToPersist.Count > 0)
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            db.Candidates.AddRange(candidatesToPersist);
            await db.SaveChangesAsync();

            foreach (var (candidate, scored) in stagedScores)
            {
                db.CandidateScores.Add(new CandidateScore
                {
                    CandidateId = candidate.Id,
                    JobId = jobId,
                    Score = scored.Score,
                    Rank = 0,
                    MatchedKeywords = string.Join(",", scored.AllMatched),
                    MissingKeywords = string.Join(",", scored.AllMissing),
                    TotalJobKeywords = scored.TotalKeywords,
                    CoreMatchedKeywords = string.Join(",", scored.CoreMatched),
                    CoreMissingKeywords = string.Join(",", scored.CoreMissing),
                    SecondaryMatchedKeywords = string.Join(",", scored.SecondaryMatched),
                    SecondaryMissingKeywords = string.Join(",", scored.SecondaryMissing),
                    HardFilters = string.Join(",", scored.HardFilters),
                    ScoreReasons = string.Join(",", scored.Explanations),
                    TotalCoreKeywords = scored.TotalCoreKeywords,
                    TotalSecondaryKeywords = scored.TotalSecondaryKeywords,
                    ScoredAt = DateTime.UtcNow
                });

                successEntries.Add((candidate.Id, candidate.FileName, scored.Score));
            }

            await db.SaveChangesAsync();
            await RecomputeRanksAsync(jobId);
            await tx.CommitAsync();

            var candidateRanks = await db.CandidateScores
                .Where(s => s.JobId == jobId)
                .ToDictionaryAsync(s => s.CandidateId, s => s.Rank);

            results.AddRange(successEntries.Select(s => new CandidateUploadFileResult(
                s.FileName,
                "completed",
                "Processed successfully.",
                s.CandidateId,
                s.Score,
                candidateRanks.GetValueOrDefault(s.CandidateId)
            )));
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

