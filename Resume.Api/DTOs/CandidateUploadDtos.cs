namespace Resume.Api.DTOs;

public record CandidateUploadFileResult(
    string FileName,
    string Status,
    string Message,
    int? CandidateId,
    int? Score,
    int? Rank
);

public record CandidateUploadResponse(
    int JobId,
    int ProcessedCount,
    int FailedCount,
    List<CandidateUploadFileResult> Files
);

