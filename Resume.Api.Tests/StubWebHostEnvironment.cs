using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Resume.Api.Tests;

/// <summary>Minimal <see cref="IWebHostEnvironment"/> for tests; only <see cref="ContentRootPath"/> is used by <see cref="Resume.Api.Services.Scoring.SkillOntology"/>.</summary>
internal sealed class StubWebHostEnvironment : IWebHostEnvironment
{
    public StubWebHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
        ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
    }

    public string ApplicationName { get; set; } = "Resume.Api.Tests";
    public IFileProvider ContentRootFileProvider { get; set; }
    public string ContentRootPath { get; set; }
    public string WebRootPath { get; set; } = string.Empty;
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string EnvironmentName { get; set; } = Environments.Development;
}
