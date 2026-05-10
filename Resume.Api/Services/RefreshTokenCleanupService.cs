using Microsoft.EntityFrameworkCore;
using Resume.Api.Data;
using Resume.Api.Models;

namespace Resume.Api.Services;

public class RefreshTokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<RefreshTokenCleanupService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DeleteExpiredTokensAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task DeleteExpiredTokensAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTimeOffset.UtcNow;
            var expired = await db.Set<RefreshToken>()
                .Where(r => r.ExpiresAt < now)
                .ToListAsync(ct);

            if (expired.Count == 0)
                return;

            db.Set<RefreshToken>().RemoveRange(expired);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Deleted {Count} expired refresh token(s).", expired.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error occurred while cleaning up expired refresh tokens.");
        }
    }
}
