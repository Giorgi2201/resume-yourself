using System.ComponentModel.DataAnnotations;

namespace Resume.Api.DTOs;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record RegisterRequest(
    [Required, EmailAddress, MaxLength(256)] string Email,
    [Required, MinLength(10), MaxLength(128)] string Password,
    [Required] string ConfirmPassword
);

public record VerifyEmailRequest(
    [Required, EmailAddress] string Email,
    [Required] string Token
);

public record ResendVerificationRequest(
    [Required, EmailAddress] string Email
);

public record RefreshTokenRequest(
    [Required] string RefreshToken
);

public record RevokeRequest(string? RefreshToken);

public record AuthTokensResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    string Email,
    IReadOnlyList<string> Roles
);

public record MessageResponse(string Message);
