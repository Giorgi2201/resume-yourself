namespace Resume.Api.Services;

public interface ICvParserService
{
    Task<ParsedCv> ParseAsync(IFormFile file);
}

public record ParsedCv(
    string RawText,
    string Name,
    string Email,
    List<string> ExtractedSkills,
    string FirstName,
    string LastName,
    double NameConfidenceScore,
    string NameSource
);
