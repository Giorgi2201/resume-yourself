using Microsoft.AspNetCore.Identity;

namespace Resume.Api.Models;

public class ApplicationUser : IdentityUser
{
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}
