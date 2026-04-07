using Resume.Api.Services;

namespace Resume.Api.Tests;

public class ScoringServiceTests
{
    private readonly ScoringService _service = new();

    [Fact]
    public void Matches_Synonyms_And_Abbreviations_For_Core_Skills()
    {
        var jd = """
            Requirements:
            - JavaScript
            - TypeScript
            - Google Tag Manager
            - GA4
            """;

        var cv = """
            Frontend Developer with 3+ years experience.
            Skills: JS, TS, GTM, Google Analytics 4
            """;

        var result = _service.Score(cv, jd);

        Assert.True(result.Score >= 75, $"Score={result.Score}; core=[{string.Join(",", result.CoreMatched)}]; missing=[{string.Join(",", result.CoreMissing)}]");
        Assert.Contains("javascript", result.CoreMatched);
        Assert.Contains("typescript", result.CoreMatched);
        Assert.Contains("google tag manager", result.CoreMatched);
        Assert.Contains("ga4", result.CoreMatched);
    }

    [Fact]
    public void Gives_Partial_Credit_For_Related_Terms_Instead_Of_Full_Penalty()
    {
        var jd = """
            Requirements:
            - CRO
            - PPC landing pages
            """;

        var cv = """
            Digital marketer experienced in conversion rate optimization and landing pages.
            Built paid media campaigns and improved funnel conversion.
            """;

        var result = _service.Score(cv, jd);

        Assert.True(result.Score >= 60, $"Score={result.Score}; core=[{string.Join(",", result.CoreMatched)}]; missing=[{string.Join(",", result.CoreMissing)}]");
        Assert.Contains(result.CoreMatched, m => m.Contains("cro", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NiceToHave_Missing_DoesNot_Destroy_Strong_Core_Score()
    {
        var jd = """
            Requirements:
            - HTML
            - CSS
            - JavaScript
            - WordPress
            Nice to have:
            - Figma
            - Jira
            """;

        var cv = """
            Web developer with 2+ years experience.
            Skills: HTML5, CSS3, JS, WordPress.
            Built fast mobile-first landing pages.
            """;

        var result = _service.Score(cv, jd);

        Assert.True(result.Score >= 70, $"Score={result.Score}; core=[{string.Join(",", result.CoreMatched)}]; missing=[{string.Join(",", result.CoreMissing)}]");
        Assert.Contains("figma", result.SecondaryMissing);
        Assert.Contains("jira", result.SecondaryMissing);
    }

    [Fact]
    public void HardFilter_Pst_Can_Disqualify_When_Contradicted()
    {
        var jd = """
            Requirements:
            - JavaScript
            - Must work PST
            """;

        var cv = """
            Frontend developer, currently working EST timezone.
            4 years JavaScript experience.
            """;

        var result = _service.Score(cv, jd);

        Assert.True(result.Score <= 10);
        Assert.Contains(result.Explanations, r => r.Contains("Disqualified", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Experience_Years_And_Role_Similarity_Improve_Score()
    {
        var jd = """
            Requirements:
            - React
            - TypeScript
            - 3+ years of experience
            """;

        var cv = """
            Senior Frontend Engineer
            5+ years experience building React and TypeScript apps.
            """;

        var result = _service.Score(cv, jd);

        Assert.True(result.Score >= 80);
        Assert.Contains(result.Explanations, r => r.Contains("Experience years", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Sparse_Cv_Gets_Confidence_Penalty()
    {
        var jd = """
            Requirements:
            - Python
            - SQL
            """;

        var cv = "Python SQL";
        var result = _service.Score(cv, jd);

        Assert.Contains(result.Explanations, r => r.Contains("Confidence penalty", StringComparison.OrdinalIgnoreCase));
    }
}
