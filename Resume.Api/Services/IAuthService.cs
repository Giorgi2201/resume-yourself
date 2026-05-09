using Resume.Api.DTOs;

namespace Resume.Api.Services;

public interface IAuthService
{
    Task<MessageResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken = default);
    Task ResendVerificationEmailAsync(ResendVerificationRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokensResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokensResponse> RefreshAsync(string refreshTokenPlain, CancellationToken cancellationToken = default);
    Task RevokeAsync(string userId, string? refreshTokenPlain, CancellationToken cancellationToken = default);
    Task RevokeByRefreshTokenAsync(string refreshTokenPlain, CancellationToken cancellationToken = default);
}
