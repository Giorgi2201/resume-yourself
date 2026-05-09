namespace Resume.Api.Configuration;

public class FileUploadOptions
{
    public const string SectionName = "FileUpload";
    public long MaxFileSizeBytes { get; init; } = 5 * 1024 * 1024;
    public int MaxFilesPerRequest { get; init; } = 20;
    public HashSet<string> AllowedExtensions { get; init; } =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".docx", ".txt" };
    public HashSet<string> AllowedMimeTypes { get; init; } =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "application/pdf",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "text/plain"
        };
}
