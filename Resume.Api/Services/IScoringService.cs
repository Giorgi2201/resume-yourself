namespace Resume.Api.Services;

public interface IScoringService
{
    WeightedScoreResult Score(string cvText, string jobDescription);
}

public record WeightedScoreResult(
    int Score,
    List<string> CoreMatched,
    List<string> CoreMissing,
    List<string> SecondaryMatched,
    List<string> SecondaryMissing,
    List<string> HardFilters,
    int TotalCoreKeywords,
    int TotalSecondaryKeywords,
    List<string>? ScoreReasons = null
)
{
    public List<string> AllMatched => [.. CoreMatched, .. SecondaryMatched];
    public List<string> AllMissing => [.. CoreMissing, .. SecondaryMissing];
    public int TotalKeywords => TotalCoreKeywords + TotalSecondaryKeywords;
    public List<string> Explanations => ScoreReasons ?? [];
}
