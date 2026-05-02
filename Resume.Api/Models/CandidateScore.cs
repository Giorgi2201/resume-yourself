namespace Resume.Api.Models;

public class CandidateScore
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public int JobId { get; set; }
    public int Score { get; set; }
    public int Rank { get; set; }
    public string MatchedKeywords { get; set; } = string.Empty;
    public string MissingKeywords { get; set; } = string.Empty;
    public int TotalJobKeywords { get; set; }
    public string CoreMatchedKeywords { get; set; } = string.Empty;
    public string CoreMissingKeywords { get; set; } = string.Empty;
    public string SecondaryMatchedKeywords { get; set; } = string.Empty;
    public string SecondaryMissingKeywords { get; set; } = string.Empty;
    public string HardFilters { get; set; } = string.Empty;
    public string ScoreReasons { get; set; } = string.Empty;
    public int TotalCoreKeywords { get; set; }
    public int TotalSecondaryKeywords { get; set; }
    public DateTime ScoredAt { get; set; } = DateTime.UtcNow;

    public Candidate Candidate { get; set; } = null!;
    public Job Job { get; set; } = null!;
}
