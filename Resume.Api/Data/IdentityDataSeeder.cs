using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Resume.Api.Configuration;
using Resume.Api.Models;

namespace Resume.Api.Data;

public static class IdentityDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var options = provider.GetRequiredService<IOptions<InitialAdminOptions>>().Value;

        foreach (var role in new[] { "Admin", "User" })
        {
            if (await roleManager.RoleExistsAsync(role))
                continue;
            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
                throw new InvalidOperationException($"Failed to create role {role}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
            return;

        var email = options.Email.Trim();
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
            return;

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var create = await userManager.CreateAsync(user, options.Password);
        if (!create.Succeeded)
            throw new InvalidOperationException($"Failed to seed admin user: {string.Join(", ", create.Errors.Select(e => e.Description))}");

        var roleName = string.IsNullOrWhiteSpace(options.Role) ? "Admin" : options.Role.Trim();
        var addRole = await userManager.AddToRoleAsync(user, roleName);
        if (!addRole.Succeeded)
            throw new InvalidOperationException($"Failed to assign role: {string.Join(", ", addRole.Errors.Select(e => e.Description))}");
    }
}
