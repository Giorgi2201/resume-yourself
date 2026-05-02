namespace Resume.Api.Services;

public interface IAuditService
{
    Task LogAsync(string action, string resourceType, string resourceId, string? metadata = null);
}
