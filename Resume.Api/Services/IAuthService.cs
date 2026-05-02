using Resume.Api.DTOs;

namespace Resume.Api.Services;

public interface IAuthService
{
    Task<AuthTokensResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokensResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task RevokeAsync(string userId, string? refreshTokenPlain, CancellationToken cancellationToken = default);
    Task RevokeByRefreshTokenAsync(string refreshTokenPlain, CancellationToken cancellationToken = default);
}
