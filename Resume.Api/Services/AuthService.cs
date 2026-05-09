using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Resume.Api.Configuration;
using Resume.Api.Data;
using Resume.Api.DTOs;
using Resume.Api.Exceptions;
using Resume.Api.Models;

namespace Resume.Api.Services;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    IOptions<JwtOptions> jwtOptions,
    IOptions<EmailOptions> emailOptions,
    IEmailService emailService) : IAuthService
{
    private readonly JwtOptions _jwt = jwtOptions.Value;
    private readonly EmailOptions _email = emailOptions.Value;

    // ──────────────────────────────────────────────────────────────
    // Registration
    // ──────────────────────────────────────────────────────────────

    public async Task<MessageResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.Password != request.ConfirmPassword)
            throw new ApiException("Passwords do not match.", StatusCodes.Status400BadRequest);

        var email = request.Email.Trim().ToLowerInvariant();

        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            // Don't reveal whether the account exists — same success message.
            return new MessageResponse("If that address is new, a verification email has been sent.");
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = false
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join(" ", createResult.Errors.Select(e => e.Description));
            throw new ApiException(errors, StatusCodes.Status422UnprocessableEntity);
        }

        await userManager.AddToRoleAsync(user, "User");

        await SendVerificationLinkAsync(user, cancellationToken);

        return new MessageResponse("Registration successful. Please check your email to verify your account.");
    }

    // ──────────────────────────────────────────────────────────────
    // Email verification
    // ──────────────────────────────────────────────────────────────

    public async Task VerifyEmailAsync(
        VerifyEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            throw new ApiException("Invalid verification link.", StatusCodes.Status400BadRequest);

        if (user.EmailConfirmed)
            throw new ApiException("This email address is already verified.", StatusCodes.Status400BadRequest);

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
            throw new ApiException(
                "The verification link is invalid or has expired.",
                StatusCodes.Status400BadRequest);
    }

    public async Task ResendVerificationEmailAsync(
        ResendVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());

        // Always return success to avoid user enumeration.
        if (user is null || user.EmailConfirmed)
            return;

        await SendVerificationLinkAsync(user, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────
    // Login
    // ──────────────────────────────────────────────────────────────

    public async Task<AuthTokensResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null)
            throw new ApiException("Invalid email or password.", StatusCodes.Status401Unauthorized);

        if (await userManager.IsLockedOutAsync(user))
            throw new ApiException("Account is locked. Try again later.", StatusCodes.Status403Forbidden);

        var passwordValid = await userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValid)
        {
            await userManager.AccessFailedAsync(user);
            throw new ApiException("Invalid email or password.", StatusCodes.Status401Unauthorized);
        }

        if (!user.EmailConfirmed)
            throw new ApiException(
                "Email address is not verified. Please check your inbox.",
                StatusCodes.Status403Forbidden);

        await userManager.ResetAccessFailedCountAsync(user);
        return await IssueTokensAsync(user, cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────
    // Refresh / Revoke
    // ──────────────────────────────────────────────────────────────

    public async Task<AuthTokensResponse> RefreshAsync(
        string refreshTokenPlain,
        CancellationToken cancellationToken = default)
    {
        var hash = HashToken(refreshTokenPlain);
        var existing = await db.Set<RefreshToken>()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing is null || !existing.IsActive)
            throw new ApiException("Invalid or expired refresh token.", StatusCodes.Status401Unauthorized);

        var user = existing.User;
        if (user is null || await userManager.IsLockedOutAsync(user))
            throw new ApiException("Invalid or expired refresh token.", StatusCodes.Status401Unauthorized);

        existing.RevokedAt = DateTime.UtcNow;
        var newPlain = GenerateSecureToken();
        var newHash = HashToken(newPlain);
        existing.ReplacedByTokenHash = newHash;

        var refreshExpiry = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays);
        db.Set<RefreshToken>().Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = newHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refreshExpiry
        });

        await db.SaveChangesAsync(cancellationToken);

        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var access = await CreateAccessTokenAsync(user, roles);
        return new AuthTokensResponse(
            access.Token,
            access.ExpiresAtUtc,
            newPlain,
            refreshExpiry,
            user.Email ?? user.UserName ?? user.Id,
            roles);
    }

    public async Task RevokeAsync(
        string userId,
        string? refreshTokenPlain,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(refreshTokenPlain))
        {
            var hash = HashToken(refreshTokenPlain);
            var token = await db.Set<RefreshToken>()
                .FirstOrDefaultAsync(t => t.UserId == userId && t.TokenHash == hash, cancellationToken);
            if (token is not null)
                token.RevokedAt = DateTime.UtcNow;
        }
        else
        {
            var tokens = await db.Set<RefreshToken>()
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ToListAsync(cancellationToken);
            var now = DateTime.UtcNow;
            foreach (var t in tokens)
                t.RevokedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeByRefreshTokenAsync(
        string refreshTokenPlain,
        CancellationToken cancellationToken = default)
    {
        var hash = HashToken(refreshTokenPlain);
        var token = await db.Set<RefreshToken>()
            .FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null)
            return;
        token.RevokedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    // ──────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────

    private async Task SendVerificationLinkAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = Uri.EscapeDataString(token);
        var encodedEmail = Uri.EscapeDataString(user.Email!);
        var link = $"{_email.AppBaseUrl.TrimEnd('/')}/verify-email?email={encodedEmail}&token={encodedToken}";
        await emailService.SendVerificationEmailAsync(user.Email!, link, cancellationToken);
    }

    private async Task<AuthTokensResponse> IssueTokensAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var refreshPlain = GenerateSecureToken();
        var refreshHash = HashToken(refreshPlain);
        var refreshExpiry = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpiryDays);

        db.Set<RefreshToken>().Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshHash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = refreshExpiry
        });

        await db.SaveChangesAsync(cancellationToken);

        var roles = (await userManager.GetRolesAsync(user)).ToList();
        var access = await CreateAccessTokenAsync(user, roles);
        return new AuthTokensResponse(
            access.Token,
            access.ExpiresAtUtc,
            refreshPlain,
            refreshExpiry,
            user.Email ?? user.UserName ?? user.Id,
            roles);
    }

    private Task<(string Token, DateTime ExpiresAtUtc)> CreateAccessTokenAsync(
        ApplicationUser user,
        IList<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email ?? user.UserName ?? user.Id),
            new(ClaimTypes.Name, user.Email ?? user.UserName ?? user.Id)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var expires = DateTime.UtcNow.AddMinutes(_jwt.ExpiryMinutes);
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);
        return Task.FromResult((encoded, expires));
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
