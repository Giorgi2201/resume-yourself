using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Resume.Api.DTOs;
using Resume.Api.Services;

namespace Resume.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshCookieName = "refreshToken";

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return Ok(result);
    }

    [AllowAnonymous]
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(
        [FromBody] VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        await authService.VerifyEmailAsync(request, cancellationToken);
        return Ok(new MessageResponse("Email verified successfully. You can now sign in."));
    }

    [AllowAnonymous]
    [HttpPost("resend-verification")]
    public async Task<IActionResult> ResendVerification(
        [FromBody] ResendVerificationRequest request,
        CancellationToken cancellationToken)
    {
        await authService.ResendVerificationEmailAsync(request, cancellationToken);
        return Ok(new MessageResponse("If that address is registered and unverified, a new email has been sent."));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var tokens = await authService.LoginAsync(request, cancellationToken);
        AppendRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc);
        return Ok(ToPublicResponse(tokens));
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var cookieToken = Request.Cookies[RefreshCookieName];
        if (string.IsNullOrWhiteSpace(cookieToken))
            return Unauthorized(new
            {
                type = "https://httpstatuses.com/401",
                title = "Unauthorized",
                status = 401,
                detail = "Refresh token cookie is missing or expired."
            });

        var tokens = await authService.RefreshAsync(cookieToken, cancellationToken);
        AppendRefreshCookie(tokens.RefreshToken, tokens.RefreshTokenExpiresAtUtc);
        return Ok(ToPublicResponse(tokens));
    }

    /// <summary>
    /// Ends the current session. Always deletes the refresh cookie.
    /// If authenticated, revokes all tokens for the user.
    /// If unauthenticated, revokes the specific token in the cookie (if present).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var cookieToken = Request.Cookies[RefreshCookieName];

        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
                await authService.RevokeAsync(userId, cookieToken, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(cookieToken))
        {
            await authService.RevokeByRefreshTokenAsync(cookieToken, cancellationToken);
        }

        DeleteRefreshCookie();
        return NoContent();
    }

    // ── Cookie helpers ────────────────────────────────────────────────────────

    private void AppendRefreshCookie(string token, DateTimeOffset expires) =>
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expires,
            Path = "/"
        });

    private void DeleteRefreshCookie() =>
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

    private static AccessTokenResponse ToPublicResponse(AuthTokensResponse r) =>
        new(r.AccessToken, r.AccessTokenExpiresAtUtc, r.Email, r.Roles);
}
