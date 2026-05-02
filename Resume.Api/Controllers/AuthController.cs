using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Resume.Api.DTOs;
using Resume.Api.Services;

namespace Resume.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var tokens = await authService.LoginAsync(request, cancellationToken);
        return Ok(tokens);
    }

    [AllowAnonymous]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var tokens = await authService.RefreshAsync(request, cancellationToken);
        return Ok(tokens);
    }

    /// <summary>
    /// Ends the current session. If authenticated, revokes all refresh tokens for the user (optionally only the one in the body).
    /// If not authenticated, supply <paramref name="request"/>.RefreshToken to revoke that session only (e.g. expired access token).
    /// </summary>
    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RevokeRequest? request, CancellationToken cancellationToken)
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
