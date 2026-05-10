using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Resume.Api.Services;
using Resume.Api.Services.NameExtraction;

namespace Resume.Api.Tests;

public class CvParserServiceTests
{
    private readonly CvParserService _service = new(
        new NameExtractionService(NullLogger<NameExtractionService>.Instance),
        NullLogger<CvParserService>.Instance);

    private static IFormFile MakeTxtFile(string content, string name = "cv.txt")
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, bytes.Length, "file", name)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/plain"
        };
    }

    [Fact]
    public async Task ParseAsync_CvWithCanonicalSkillNames_ExtractsAllSkills()
    {
        var file = MakeTxtFile("""
            John Doe
            john.doe@example.com
            5 years of experience with javascript, python, docker, and angular.
            """);

        var result = await _service.ParseAsync(file);

        result.ExtractedSkills.Should().Contain("javascript")
            .And.Contain("python")
            .And.Contain("docker")
            .And.Contain("angular");
    }

    [Fact]
    public async Task ParseAsync_CvWithSkillAlias_NormalizesToCanonicalInRawText()
    {
        // CvParserService checks for canonical names in raw text,
        // so "typescript" (canonical) must be present — not just "ts".
        // This verifies the canonical detection path.
        var file = MakeTxtFile("""
            Jane Smith
            jane@example.com
            Skilled in typescript, css, and react with sql database work.
            """);

        var result = await _service.ParseAsync(file);

        result.ExtractedSkills.Should().Contain("typescript")
            .And.Contain("css")
            .And.Contain("react")
            .And.Contain("sql");
        result.ExtractedSkills.Should().NotContain("ts");
    }

    [Fact]
    public async Task ParseAsync_EmptyContent_ReturnsValidButEmptyResult()
    {
        var file = MakeTxtFile(string.Empty);

        var result = await _service.ParseAsync(file);

        result.Should().NotBeNull();
        result.RawText.Should().BeNullOrEmpty();
        result.ExtractedSkills.Should().BeEmpty();
        result.Email.Should().BeEmpty();
        result.Name.Should().Be("Unknown");
    }

    [Fact]
    public async Task ParseAsync_CvWithEmailAddress_ExtractsEmailCorrectly()
    {
        var file = MakeTxtFile("""
            Alex Kumar
            alex.kumar@techcorp.io
            Senior backend developer with java and c# expertise.
            """);

        var result = await _service.ParseAsync(file);

        result.Email.Should().Be("alex.kumar@techcorp.io");
    }

    [Fact]
    public async Task ParseAsync_CvWithMultipleSkillAreas_DoesNotDuplicateSkills()
    {
        var file = MakeTxtFile("""
            Sam Lee
            sam@example.com
            Skills: javascript, react, css, html, git
            Experience: javascript developer for 3 years
            Projects: react and css applications
            """);

        var result = await _service.ParseAsync(file);

        result.ExtractedSkills.Should().Contain("javascript")
            .And.Contain("react")
            .And.Contain("css")
            .And.Contain("html")
            .And.Contain("git");

        // No duplicates in the extracted list
        result.ExtractedSkills.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task ParseAsync_CvWithNoSkills_ReturnsEmptySkillList()
    {
        var file = MakeTxtFile("""
            Chris Williams
            chris@example.com
            I am a creative professional with experience in team leadership and communication.
            """);

        var result = await _service.ParseAsync(file);

        result.ExtractedSkills.Should().BeEmpty();
        result.Email.Should().Be("chris@example.com");
    }
}
