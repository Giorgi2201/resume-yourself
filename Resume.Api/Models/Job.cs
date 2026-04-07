namespace Resume.Api.Models;

public class Job
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CandidateScore> Scores { get; set; } = [];
    public ICollection<Feedback> Feedbacks { get; set; } = [];
}
