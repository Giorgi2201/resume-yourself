using Microsoft.EntityFrameworkCore;
using Resume.Api.Data;

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
            var deleted = await db.RefreshTokens
                .Where(t => t.ExpiresAt < now)
                .ExecuteDeleteAsync(ct);

            if (deleted > 0)
                logger.LogInformation("Deleted {Count} expired refresh token(s).", deleted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Error occurred while cleaning up expired refresh tokens.");
        }
    }
}
