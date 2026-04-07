namespace Resume.Api.Models;

public class Candidate
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ParsedText { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ExtractedSkills { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public ICollection<CandidateScore> Scores { get; set; } = [];
    public ICollection<Feedback> Feedbacks { get; set; } = [];
}
