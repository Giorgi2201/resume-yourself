using System.ComponentModel.DataAnnotations;

namespace Resume.Api.DTOs;

public record CreateJobRequest(
    [Required, StringLength(200, MinimumLength = 3)] string Title,
    [Required, StringLength(10000, MinimumLength = 20)] string Description
);

public record JobResponse(int Id, string Title, string Description, DateTimeOffset CreatedAt);

public record JobSummaryResponse(
    int Id,
    string Title,
    DateTimeOffset CreatedAt,
    int TotalCandidates,
    int AverageScore,
    int TopScore,
    int ApprovedCount,
    int RejectedCount
);
