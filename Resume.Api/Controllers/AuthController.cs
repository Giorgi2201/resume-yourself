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
        return Ok(tokens);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var tokens = await authService.RefreshAsync(request, cancellationToken);
        return Ok(tokens);
    }

    /// <summary>
    /// Ends the current session. Revokes all refresh tokens for the user when authenticated,
    /// or just the supplied refresh token when the access token is missing/expired.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] RevokeRequest? request,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrEmpty(userId))
                await authService.RevokeAsync(userId, request?.RefreshToken, cancellationToken);
            return NoContent();
        }

        if (string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            return new ObjectResult(new
            {
                type = "https://httpstatuses.com/401",
                title = "Unauthorized",
                status = StatusCodes.Status401Unauthorized,
                detail = "Refresh token is required to sign out when the access token is missing or invalid.",
                traceId = HttpContext.TraceIdentifier
            })
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        await authService.RevokeByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        return NoContent();
    }
}
