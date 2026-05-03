namespace Resume.Api.Configuration;

public class EmailOptions
{
    public const string SectionName = "Email";

    public string SmtpHost { get; init; } = string.Empty;
    public int SmtpPort { get; init; } = 587;
    public string SmtpUser { get; init; } = string.Empty;
    public string SmtpPassword { get; init; } = string.Empty;
    public bool UseSsl { get; init; } = true;
    public string FromAddress { get; init; } = "noreply@example.com";
    public string FromName { get; init; } = "Resume Automation";

    /// <summary>Base URL of the Angular frontend (used to build verification links).</summary>
    public string AppBaseUrl { get; init; } = "http://localhost:4200";
}
