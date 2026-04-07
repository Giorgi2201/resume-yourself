namespace Resume.Api.Services.NameExtraction;

public interface INameExtractionService
{
    NameExtractionResult Extract(string rawText, string? email = null, string? fileName = null);
}

public record NameExtractionResult(
    string FirstName,
    string LastName,
    string FullName,
    double ConfidenceScore,
    string Source
);

