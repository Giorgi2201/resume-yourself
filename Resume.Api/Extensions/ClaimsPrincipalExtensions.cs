using System.Security.Claims;

namespace Resume.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the NameIdentifier claim value (the ASP.NET Identity user ID).
    /// Throws if the claim is missing — should only be called from [Authorize] endpoints.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id))
            throw new InvalidOperationException("Authenticated user has no NameIdentifier claim.");
        return id;
    }
}
