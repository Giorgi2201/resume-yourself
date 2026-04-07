namespace Resume.Api.DTOs;

public record CandidateResultResponse(
    int CandidateId,
    int ScoreId,
    string Name,
    string Email,
    string FileName,
    int Score,
    int Rank,
    // Weighted breakdown (computed live from CV text + JD)
    List<string> CoreMatchedKeywords,
    List<string> CoreMissingKeywords,
    List<string> SecondaryMatchedKeywords,
    List<string> SecondaryMissingKeywords,
    List<string> HardFilters,
    List<string> ScoreReasons,
    int TotalCoreKeywords,
    int TotalSecondaryKeywords,
    string? FeedbackType
);

public record FeedbackRequest(int CandidateId, int JobId, string Type);
