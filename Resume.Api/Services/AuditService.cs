using System.Security.Claims;
using System.Text.Json;
using Resume.Api.Data;
using Resume.Api.Models;

namespace Resume.Api.Services;

public class AuditService(AppDbContext db, IHttpContextAccessor httpContextAccessor) : IAuditService
{
    public async Task LogAsync(string action, string resourceType, string resourceId, string? metadata = null)
    {
        var userId = httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            UserId = userId,
            Metadata = metadata is null ? null : JsonSerializer.Serialize(new { detail = metadata }),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }
}
