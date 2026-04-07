namespace Resume.Api.DTOs;

public record CreateJobRequest(string Title, string Description);

public record JobResponse(int Id, string Title, string Description, DateTime CreatedAt);
