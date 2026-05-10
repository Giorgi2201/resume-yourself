using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Resume.Api.Configuration;
using Resume.Api.Services;

namespace Resume.Api.Tests;

public class ScoringServiceTests
{
    private readonly ScoringService _service = new(
        Options.Create(new ScoringOptions()),
        NullLogger<ScoringService>.Instance);

    // ── Core skill coverage ───────────────────────────────────────────────────

    [Fact]
    public void Score_AllCoreSkillsPresent_ReturnsHighScore()
    {
        var jd = """
            Requirements:
            - javascript
            - react
            - css
            - html
            """;

        var cv = """
            Jane Smith
            jane@example.com
            5 years of experience as a frontend developer.
            Proficient in javascript, react, css, and html.
            """;

        var result = _service.Score(cv, jd);

        result.Score.Should().BeGreaterThanOrEqualTo(70);
        result.CoreMatched.Should().Contain("javascript")
            .And.Contain("react")
            .And.Contain("css")
            .And.Contain("html");
        result.CoreMissing.Should().BeEmpty();
    }

    [Fact]
    public void Score_NoCoreSkillsPresent_ReturnsLowScore()
    {
        var jd = """
            Requirements:
            - python
            - machine learning
            - deep learning
            """;

        var cv = """
            Alex Johnson
            alex@example.com
            Frontend developer with 3 years experience.
            Expert in javascript, angular, and css.
            """;

        var result = _service.Score(cv, jd);

        result.Score.Should().BeLessThanOrEqualTo(35);   // roleSimilarity and yearsScore default to 1.0 when JD omits them
        result.CoreMissing.Should().Contain("python")
            .And.Contain("machine learning")
            .And.Contain("deep learning");
    }

    [Fact]
    public void Score_PartialCoreSkillMatch_ReturnsIntermediateScore()
    {
        var jd = """
            Requirements:
            - javascript
            - react
            - python
            - docker
            """;

        var cvAllSkills = "javascript react python docker developer 5 years";
        var cvHalfSkills = "javascript react developer 5 years";

        var fullMatch = _service.Score(cvAllSkills, jd);
        var halfMatch = _service.Score(cvHalfSkills, jd);

        halfMatch.Score.Should().BeLessThan(fullMatch.Score);
        halfMatch.CoreMatched.Count.Should().Be(2);
        halfMatch.CoreMissing.Should().Contain("python")
            .And.Contain("docker");
    }

    // ── Secondary skill contribution ──────────────────────────────────────────

    [Fact]
    public void Score_SecondarySkillsPresent_ScoresHigherThanCoreOnlyMatch()
    {
        var jd = """
            Requirements:
            - javascript
            - react
            Nice to have:
            - docker
            - aws
            """;

        var cvCoreOnly = "javascript react developer 4 years";
        var cvCoreAndSecondary = "javascript react docker aws developer 4 years";

        var coreOnly = _service.Score(cvCoreOnly, jd);
        var withSecondary = _service.Score(cvCoreAndSecondary, jd);

        withSecondary.Score.Should().BeGreaterThan(coreOnly.Score);
        withSecondary.SecondaryMatched.Should().Contain("docker")
            .And.Contain("aws");
        coreOnly.SecondaryMatched.Should().BeEmpty();
    }

    [Fact]
    public void Score_NiceToHaveMissing_DoesNotDestroyStrongCoreScore()
    {
        var jd = """
            Requirements:
            - html
            - css
            - javascript
            - wordpress
            Nice to have:
            - figma
            - jira
            """;

        var cv = """
            Web developer with 2+ years experience.
            Skills: html5, css3, javascript, wordpress.
            Built fast mobile-first landing pages.
            """;

        var result = _service.Score(cv, jd);

        result.Score.Should().BeGreaterThanOrEqualTo(70);
        result.SecondaryMissing.Should().Contain("figma")
            .And.Contain("jira");
    }

    // ── Years of experience ───────────────────────────────────────────────────

    [Fact]
    public void Score_InsufficientYearsExperience_ScoresLowerThanMatchingYears()
    {
        var jd = """
            Requirements:
            - javascript
            - react
            5 years of experience required.
            """;

        var cvShortExperience = "javascript react developer 2 years";
        var cvMatchingExperience = "javascript react developer 5 years";

        var insufficient = _service.Score(cvShortExperience, jd);
        var matching = _service.Score(cvMatchingExperience, jd);

        matching.Score.Should().BeGreaterThan(insufficient.Score);
    }

    [Fact]
    public void Score_ExperienceYearsAndRoleSimilarity_ImproveScore()
    {
        var jd = """
            Requirements:
            - react
            - typescript
            3 years of experience required.
            """;

        var cv = """
            Senior Frontend Engineer
            5+ years experience building react and typescript apps.
            """;

        var result = _service.Score(cv, jd);

        result.Score.Should().BeGreaterThanOrEqualTo(80);
        result.Explanations.Should().Contain(r => r.Contains("Experience years", StringComparison.OrdinalIgnoreCase));
    }

    // ── Skill alias normalisation ─────────────────────────────────────────────

    [Fact]
    public void Score_SkillAliasInCv_IsNormalizedToCanonicalForm()
    {
        var jd = """
            Requirements:
            - javascript
            - typescript
            - google tag manager
            - ga4
            """;

        var cv = """
            Frontend Developer with 3+ years experience.
            Skills: JS, TS, GTM, Google Analytics 4
            """;

        var result = _service.Score(cv, jd);

        result.Score.Should().BeGreaterThanOrEqualTo(75,
            because: $"core=[{string.Join(",", result.CoreMatched)}]; missing=[{string.Join(",", result.CoreMissing)}]");
        result.CoreMatched.Should().Contain("javascript")
            .And.Contain("typescript")
            .And.Contain("google tag manager")
            .And.Contain("ga4");
    }

    [Fact]
    public void Score_RelatedSkillInCv_GivesPartialCreditInsteadOfFullPenalty()
    {
        var jd = """
            Requirements:
            - cro
            - ppc landing pages
            """;

        var cv = """
            Digital marketer experienced in conversion rate optimization and landing pages.
            Built paid media campaigns and improved funnel conversion.
            """;

        var result = _service.Score(cv, jd);

        result.Score.Should().BeGreaterThanOrEqualTo(60,
            because: $"core=[{string.Join(",", result.CoreMatched)}]; missing=[{string.Join(",", result.CoreMissing)}]");
        result.CoreMatched.Should().Contain(m => m.Contains("cro", StringComparison.OrdinalIgnoreCase));
    }

    // ── Hard filter rejection ─────────────────────────────────────────────────

    [Fact]
    public void Score_VisaSponsorshipHardFilter_DisqualifiesCandidateWhoNeedsSponsorship()
    {
        var jd = """
            Requirements:
            - javascript
            - react
            visa sponsorship not available
            """;

        var cv = """
            Maria Garcia
            I require visa sponsorship to be employed.
            Experienced javascript and react developer with 4 years of experience.
            """;

        var result = _service.Score(cv, jd);

        result.HardFilters.Should().NotBeEmpty();
        result.HardFilters.Should().ContainMatch("*Hard filter failed*");
        result.Score.Should().BeLessThanOrEqualTo(10);
        result.Explanations.Should().Contain(r => r.Contains("Disqualified", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Score_PstTimezoneHardFilter_DisqualifiesCandidateInOtherTimezone()
    {
        var jd = """
            Requirements:
            - javascript
            Must work PST
            """;

        var cv = """
            Frontend developer currently working EST timezone.
            4 years javascript experience.
            """;

        var result = _service.Score(cv, jd);

        result.Score.Should().BeLessThanOrEqualTo(10);
        result.Explanations.Should().Contain(r => r.Contains("Disqualified", StringComparison.OrdinalIgnoreCase));
    }

    // ── Confidence adjustments ────────────────────────────────────────────────

    [Fact]
    public void Score_SparseCv_AppliesConfidencePenalty()
    {
        var jd = """
            Requirements:
            - python
            - sql
            """;

        var cv = "python sql"; // Very short — below 120 word threshold

        var result = _service.Score(cv, jd);

        result.Explanations.Should().Contain(r => r.Contains("Confidence penalty", StringComparison.OrdinalIgnoreCase));
    }
}
