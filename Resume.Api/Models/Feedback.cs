namespace Resume.Api.Models;

public class Feedback
{
    public int Id { get; set; }
    public int CandidateId { get; set; }
    public int JobId { get; set; }
    public FeedbackType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Candidate Candidate { get; set; } = null!;
    public Job Job { get; set; } = null!;
}

public enum FeedbackType
{
    Approved,
    Rejected
}
