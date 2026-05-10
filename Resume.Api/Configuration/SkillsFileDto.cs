namespace Resume.Api.Configuration;

/// <summary>Root DTO for Configuration/skills.json deserialization.</summary>
public sealed class SkillsFileDto
{
    public List<SkillEntry> Skills { get; init; } = [];
    public Dictionary<string, List<string>> RelatedSkills { get; init; } = [];
}
