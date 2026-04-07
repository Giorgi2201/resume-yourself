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
    public DateTime ScoredAt { get; set; } = DateTime.UtcNow;

    public Candidate Candidate { get; set; } = null!;
    public Job Job { get; set; } = null!;
}
