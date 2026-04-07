using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using Resume.Api.Services.NameExtraction;
using UglyToad.PdfPig;

namespace Resume.Api.Services;

public partial class CvParserService(INameExtractionService nameExtractor) : ICvParserService
{
    private static readonly HashSet<string> KnownSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        "javascript", "typescript", "python", "java", "c#", "c++", "c", "go", "rust",
        "ruby", "php", "swift", "kotlin", "scala", "r", "matlab", "perl", "haskell",
        "dart", "elixir", "clojure", "f#", "vb.net", "groovy", "lua", "shell", "bash",
        "react", "angular", "vue", "svelte", "next.js", "nuxt", "gatsby", "ember",
        "html", "css", "sass", "scss", "tailwind", "bootstrap", "material ui",
        "redux", "mobx", "rxjs", "graphql", "webpack", "vite",
        "node.js", "express", "fastapi", "django", "flask", "spring", "asp.net",
        ".net", "laravel", "rails", "gin", "fiber", "nestjs", "hapi",
        "sql", "postgresql", "mysql", "sqlite", "mongodb", "redis", "elasticsearch",
        "cassandra", "dynamodb", "firebase", "supabase", "oracle", "mssql",
        "aws", "azure", "gcp", "docker", "kubernetes", "terraform", "ansible",
        "jenkins", "github actions", "gitlab ci", "ci/cd", "linux", "nginx",
        "prometheus", "grafana", "helm", "argo", "pulumi",
        "machine learning", "deep learning", "tensorflow", "pytorch", "keras",
        "scikit-learn", "pandas", "numpy", "spark", "airflow", "kafka", "hadoop",
        "data science", "nlp", "computer vision", "llm", "openai", "langchain",
        "git", "agile", "scrum", "kanban", "jira", "confluence", "figma",
        "microservices", "rest", "api", "grpc", "oauth", "jwt",
        "tdd", "bdd", "unit testing", "integration testing", "solid", "clean architecture",
        "android", "ios", "react native", "flutter", "xamarin",
    };

    public async Task<ParsedCv> ParseAsync(IFormFile file)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var rawText = extension switch
        {
            ".pdf" => await ExtractPdfTextAsync(file),
            ".docx" => await ExtractDocxTextAsync(file),
            ".txt" => await ExtractPlainTextAsync(file),
            _ => await ExtractPlainTextAsync(file)
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
        return [.. KnownSkills.Where(skill => lower.Contains(skill.ToLowerInvariant()))];
    }

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b")]
    private static partial Regex EmailRegex();
}

