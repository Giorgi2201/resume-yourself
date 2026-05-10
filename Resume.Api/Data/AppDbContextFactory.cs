using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Resume.Api.Data;

/// <summary>
/// Design-time factory for EF Core CLI. Set <c>EFCORE_PG_DESIGN=1</c> when adding migrations so
/// the model is snapshotted for PostgreSQL (identity columns, proper types). Omit it for
/// <c>dotnet ef database update</c> against local SQLite — configuration is loaded from appsettings.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();

        var usePostgresSnapshot = string.Equals(
            Environment.GetEnvironmentVariable("EFCORE_PG_DESIGN"),
            "1",
            StringComparison.Ordinal);

        if (usePostgresSnapshot)
        {
            // Connection is not opened during `migrations add`; only the Npgsql provider is required.
            optionsBuilder.UseNpgsql(
                "Host=127.0.0.1;Port=65432;Database=ef_design_placeholder;Username=postgres;Password=unused");
        }
        else
        {
            var projectDir = ResolveProjectDirectory();
            // JSON only (no environment variables) so design-time matches repo files and
            // stray ConnectionStrings__* in the shell does not override SQLite with Npgsql.
            // To target another database: dotnet ef database update --connection "..."
            var configuration = new ConfigurationBuilder()
                .SetBasePath(projectDir)
                .AddJsonFile("appsettings.json", optional: false)
                .AddJsonFile("appsettings.Development.json", optional: true)
                .Build();

            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' is missing. Check appsettings.json.");

            if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase))
                optionsBuilder.UseNpgsql(connectionString);
            else
                optionsBuilder.UseSqlite(connectionString);
        }

        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveProjectDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Resume.Api.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
