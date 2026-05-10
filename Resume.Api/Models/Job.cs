namespace Resume.Api.Models;

public class Job
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser User { get; set; } = null!;
    public ICollection<CandidateScore> Scores { get; set; } = [];
    public ICollection<Feedback> Feedbacks { get; set; } = [];
}
