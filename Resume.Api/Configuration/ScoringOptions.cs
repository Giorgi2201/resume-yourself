namespace Resume.Api.Configuration;

public class ScoringOptions
{
    public const string SectionName = "Scoring";
    public double BaseScore { get; init; } = 5;
    public double CoreSkillsWeight { get; init; } = 65;
    public double RoleSimilarityWeight { get; init; } = 10;
    public double YearsExperienceWeight { get; init; } = 10;
    public double SecondarySkillsWeight { get; init; } = 10;
}
