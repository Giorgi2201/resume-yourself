namespace Resume.Api.Configuration;

public class SkillEntry
{
    public string Canonical { get; init; } = string.Empty;
    public List<string> Aliases { get; init; } = [];
}
