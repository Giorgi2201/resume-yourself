namespace Resume.Api.Configuration;

/// <summary>
/// Optional one-time bootstrap user (use user-secrets or environment variables in production).
/// </summary>
public class InitialAdminOptions
{
    public const string SectionName = "InitialAdmin";
    public bool Enabled { get; init; }
    public string Email { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string Role { get; init; } = "Admin";
}
