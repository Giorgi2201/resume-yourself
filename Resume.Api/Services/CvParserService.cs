using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using Resume.Api.Exceptions;
using Resume.Api.Services.NameExtraction;
using Resume.Api.Services.Scoring;
using UglyToad.PdfPig;

namespace Resume.Api.Services;

public partial class CvParserService(INameExtractionService nameExtractor) : ICvParserService
{

    public async Task<ParsedCv> ParseAsync(IFormFile file)
    {
        try
        {
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var rawText = extension switch
            {
                ".pdf" => await ExtractPdfTextAsync(file),
                ".docx" => await ExtractDocxTextAsync(file),
                ".txt" => await ExtractPlainTextAsync(file),
                _ => throw new FileParsingException($"Unsupported file extension '{extension}'.")
            };

            var email = ExtractEmail(rawText);
            var skills = ExtractSkills(rawText);
            var extractedName = nameExtractor.Extract(rawText, email, file.FileName);

            return new ParsedCv(
                RawText: rawText,
                Name: extractedName.FullName,
                Email: email,
                ExtractedSkills: skills,
                FirstName: extractedName.FirstName,
                LastName: extractedName.LastName,
                NameConfidenceScore: extractedName.ConfidenceScore,
                NameSource: extractedName.Source
            );
        }
        catch (FileParsingException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new FileParsingException($"Failed to parse '{file.FileName}'.", ex);
        }
    }

    private static async Task<string> ExtractPdfTextAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        var sb = new StringBuilder();
        using var pdf = PdfDocument.Open(ms.ToArray());
        foreach (var page in pdf.GetPages())
            sb.AppendLine(page.Text);

        return sb.ToString();
    }

    private static async Task<string> ExtractDocxTextAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        ms.Position = 0;

        var sb = new StringBuilder();
        using var doc = WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body is null) return string.Empty;

        foreach (var para in body.Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>())
            sb.AppendLine(para.InnerText);

        return sb.ToString();
    }

    private static async Task<string> ExtractPlainTextAsync(IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        return await reader.ReadToEndAsync();
    }

    private static string ExtractEmail(string text)
    {
        var match = EmailRegex().Match(text);
        return match.Success ? match.Value : string.Empty;
    }

    private static List<string> ExtractSkills(string text)
    {
        var lower = text.ToLowerInvariant();
        return [.. SkillOntology.AllCanonicals.Where(canonical => lower.Contains(canonical))];
    }

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b")]
    private static partial Regex EmailRegex();
}

