namespace Resume.Api.Configuration;

public class FileUploadOptions
{
    public const string SectionName = "FileUpload";
    public long MaxFileSizeBytes { get; init; } = 5 * 1024 * 1024;
    public int MaxFilesPerRequest { get; init; } = 20;
    public List<string> AllowedExtensions { get; init; } = [".pdf", ".docx", ".txt"];
    public List<string> AllowedMimeTypes { get; init; } =
    [
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain"
    ];
}
