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

// Internal service → controller contract (includes refresh token for cookie-setting).
public record AuthTokensResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    string Email,
    IReadOnlyList<string> Roles
);

// Public API response — refresh token is never sent to the client in the body.
public record AccessTokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    string Email,
    IReadOnlyList<string> Roles
);

public record MessageResponse(string Message);
