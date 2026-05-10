using Microsoft.Extensions.Logging.Abstractions;
using Resume.Api.Services.NameExtraction;

namespace Resume.Api.Tests;

public class NameExtractionServiceTests
{
    private readonly NameExtractionService _service =
        new(NullLogger<NameExtractionService>.Instance);

    [Fact]
    public void Extracts_Name_From_Header_With_High_Confidence()
    {
        var cv = """
            Jean-Paul Dubois
            Senior Web Developer
            jean.dubois@gmail.com
            +1 555 123 4567

            Experience
            ...
            """;

        var result = _service.Extract(cv);

        Assert.Equal("Jean-Paul", result.FirstName);
        Assert.Equal("Dubois", result.LastName);
        Assert.Equal("Jean-Paul Dubois", result.FullName);
        Assert.True(result.ConfidenceScore >= 0.7);
        Assert.Equal("header", result.Source);
    }

    [Fact]
    public void Extracts_Name_From_Labeled_Field()
    {
        var cv = """
            Profile
            Full Name: Giorgi Kapanadze
            Email: g.kapanadze@gmail.com
            """;

        var result = _service.Extract(cv);

        Assert.Equal("Giorgi", result.FirstName);
        Assert.Equal("Kapanadze", result.LastName);
        Assert.Equal("Giorgi Kapanadze", result.FullName);
        Assert.Equal("labeled-field", result.Source);
    }

    [Fact]
    public void Extracts_Name_From_Email_When_Header_Is_Unclear()
    {
        var cv = """
            PROFILE
            WEB DEVELOPER
            Contact: john.doe@gmail.com
            Skills: JavaScript, Angular, CSS
            """;

        var result = _service.Extract(cv);

        Assert.Equal("John", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal("John Doe", result.FullName);
        Assert.Equal("email", result.Source);
    }

    [Fact]
    public void Extracts_Name_From_LinkedIn_Slug()
    {
        var cv = """
            Contact links
            https://www.linkedin.com/in/alex-smith-dev
            Portfolio: https://example.com
            """;

        var result = _service.Extract(cv);

        Assert.Equal("Alex", result.FirstName);
        Assert.Equal("Smith", result.LastName);
        Assert.Equal("Alex Smith", result.FullName);
        Assert.Equal("linkedin", result.Source);
    }

    [Fact]
    public void Rejects_Job_Titles_And_Returns_Unknown_When_No_Valid_Name()
    {
        var cv = """
            Senior Frontend Developer
            JavaScript Angular TypeScript
            Experience
            Company A
            Company B
            """;

        var result = _service.Extract(cv);

        Assert.Equal("Unknown", result.FullName);
        Assert.Equal("none", result.Source);
        Assert.Equal(0, result.ConfidenceScore);
    }

    [Fact]
    public void Supports_Single_Word_Name()
    {
        var cv = """
            Madonna
            Email: madonna@example.com
            """;

        var result = _service.Extract(cv);

        Assert.Equal("Madonna", result.FirstName);
        Assert.Equal(string.Empty, result.LastName);
        Assert.Equal("Madonna", result.FullName);
    }

    [Fact]
    public void Handles_Noisy_Email_With_Numbers_By_Falling_Back_Safely()
    {
        var cv = """
            Profile
            Contact: john1987.doe77@gmail.com
            Skills: React, TypeScript
            """;

        var result = _service.Extract(cv);

        // Do not guess from noisy email tokens with digits.
        Assert.NotEqual("John Doe", result.FullName);
    }
}

