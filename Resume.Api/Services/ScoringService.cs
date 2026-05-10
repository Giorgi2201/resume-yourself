using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Resume.Api.Configuration;
using Resume.Api.Services.Scoring;

namespace Resume.Api.Services;

/// <summary>
/// Deterministic, explainable scoring engine:
/// 1) Hard filters first
/// 2) Canonical skill relevance (core)
/// 3) Experience relevance (role+duration)
/// 4) Nice-to-have relevance
/// 5) Confidence adjustments
/// 6) Final cap 0..100
/// </summary>
public partial class ScoringService(
    IOptions<ScoringOptions> scoringOptions,
    ILogger<ScoringService> logger,
    SkillOntology skillOntology) : IScoringService
{
    private readonly IOptions<ScoringOptions> _scoringOptions = scoringOptions;
    private readonly SkillOntology _skillOntology = skillOntology;

    public WeightedScoreResult Score(string cvText, string jobDescription)
    {
        var reasons = new List<string>();
        var job = ParseJobDescription(jobDescription);
        var candidate = BuildCandidateProfile(cvText);

        // 1) Hard filters first.
        var hardFilterFlags = EvaluateHardFilters(job.HardFilters, candidate, reasons);
        if (hardFilterFlags.Any(x => x.IsDisqualifying))
        {
            var disqReasons = hardFilterFlags.Where(x => x.IsDisqualifying)
                .Select(x => x.Reason).ToList();
            logger.LogWarning("Candidate rejected by hard filter(s): {Reasons}", string.Join("; ", disqReasons));
            return new WeightedScoreResult(
                Score: 5,
                CoreMatched: [],
                CoreMissing: [.. job.CoreSkills.OrderBy(x => x)],
                SecondaryMatched: [],
                SecondaryMissing: [.. job.NiceToHaveSkills.OrderBy(x => x)],
                HardFilters: disqReasons,
                TotalCoreKeywords: job.CoreSkills.Count,
                TotalSecondaryKeywords: job.NiceToHaveSkills.Count,
                ScoreReasons: ["Disqualified by hard filter(s).", .. disqReasons]
            );
        }

        // 2) Core skill relevance (high impact)
        var coreMatched = new List<string>();
        var coreMissing = new List<string>();
        double coreRatio = CalculateSkillMatchRatio(job.CoreSkills, candidate.CanonicalSkills, coreMatched, coreMissing);
        reasons.Add($"Core skill coverage: {coreMatched.Count}/{Math.Max(1, job.CoreSkills.Count)}.");

        // 3) Experience relevance (role + years)
        bool hasRoleRequirement = job.RoleSignals.Count > 0;
        bool hasYearsRequirement = job.RequiredYears > 0;
        double roleSimilarity = hasRoleRequirement
            ? CalculateRoleSimilarity(job.RoleSignals, candidate.RoleSignals)
            : 1.0;
        double yearsScore = hasYearsRequirement
            ? CalculateExperienceScore(job.RequiredYears, candidate.YearsExperience)
            : 1.0;
        reasons.Add($"Role similarity: {(int)Math.Round(roleSimilarity * 100)}%.");
        if (job.RequiredYears > 0)
            reasons.Add($"Experience years: candidate {candidate.YearsExperience:0.#} vs required {job.RequiredYears:0.#}.");

        // 4) Nice-to-have (smaller impact)
        var secondaryMatched = new List<string>();
        var secondaryMissing = new List<string>();
        double secondaryRatio = CalculateSkillMatchRatio(job.NiceToHaveSkills, candidate.CanonicalSkills, secondaryMatched, secondaryMissing);
        if (job.NiceToHaveSkills.Count > 0)
            reasons.Add($"Nice-to-have coverage: {secondaryMatched.Count}/{job.NiceToHaveSkills.Count}.");

        // 5) Confidence adjustments
        int confidenceAdjust = CalculateConfidenceAdjustment(candidate, reasons);

        // 6) Final weighted score 0..100
        var w = _scoringOptions.Value;
        double weighted = w.BaseScore
            + (coreRatio * w.CoreSkillsWeight)
            + (roleSimilarity * w.RoleSimilarityWeight)
            + (yearsScore * w.YearsExperienceWeight)
            + (secondaryRatio * w.SecondarySkillsWeight);

        int final = (int)Math.Round(weighted + confidenceAdjust);
        final = Math.Clamp(final, 0, 100);

        logger.LogDebug(
            "Scoring complete: final={Score}, core={CoreRatio:P1}, role={RoleSimilarity:P1}, years={YearsScore:P1}, secondary={SecondaryRatio:P1}, confidence={ConfidenceAdjust:+0;-0}",
            final, coreRatio, roleSimilarity, yearsScore, secondaryRatio, confidenceAdjust);

        // 7) Explainability output
        return new WeightedScoreResult(
            Score: final,
            CoreMatched: [.. coreMatched.OrderBy(x => x)],
            CoreMissing: [.. coreMissing.OrderBy(x => x)],
            SecondaryMatched: [.. secondaryMatched.OrderBy(x => x)],
            SecondaryMissing: [.. secondaryMissing.OrderBy(x => x)],
            HardFilters: [.. hardFilterFlags.Select(x => x.Reason)],
            TotalCoreKeywords: job.CoreSkills.Count,
            TotalSecondaryKeywords: job.NiceToHaveSkills.Count,
            ScoreReasons: reasons
        );
    }

    private ParsedJobProfile ParseJobDescription(string jd)
    {
        var lines = jd.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var core = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var secondary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hardFilters = new List<string>();
        var roleSignals = ExtractRoleSignals(jd);

        double requiredYears = ExtractRequiredYears(jd);
        var tier = Tier.Core;

        foreach (var line in lines)
        {
            var lower = TextNormalizer.Normalize(line);

            if (IsSecondaryHeader(lower) || IsSecondarySignal(lower))
            {
                tier = Tier.Secondary;
                continue;
            }

            if (IsCoreHeader(lower))
            {
                tier = Tier.Core;
                continue;
            }

            if (IsHardFilterLine(lower))
            {
                hardFilters.Add(line);
                continue;
            }

            var lineSkills = ExtractCanonicalSkills(line);
            foreach (var skill in lineSkills)
            {
                if (tier == Tier.Secondary) secondary.Add(skill);
                else core.Add(skill);
            }
        }

        // Ensure secondary does not duplicate core
        foreach (var k in core) secondary.Remove(k);

        return new ParsedJobProfile(core, secondary, hardFilters, requiredYears, roleSignals);
    }

    private CandidateProfile BuildCandidateProfile(string cvText)
    {
        var canonicalSkills = ExtractCanonicalSkills(cvText);
        var years = ExtractCandidateYears(cvText);
        var roleSignals = ExtractRoleSignals(cvText);
        var words = TextNormalizer.Tokenize(cvText).Count;
        return new CandidateProfile(canonicalSkills, years, roleSignals, words, cvText);
    }

    private HashSet<string> ExtractCanonicalSkills(string text)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (canonical, aliases) in _skillOntology.CanonicalSkills)
        {
            if (aliases.Any(alias => AliasMatches(text, alias)))
                found.Add(canonical);
        }
        return found;
    }

    private static bool AliasMatches(string text, string alias)
    {
        var normalizedAlias = TextNormalizer.Normalize(alias);
        if (string.IsNullOrWhiteSpace(normalizedAlias)) return false;

        // Phrase match first
        if (TextNormalizer.ContainsNormalizedPhrase(text, normalizedAlias))
            return true;

        // Token-level fuzzy for single tokens
        if (!normalizedAlias.Contains(' '))
            return TextNormalizer.FuzzyContainsToken(text, normalizedAlias);

        // For phrase aliases, allow partial token overlap if phrase is two words
        var aliasTokens = normalizedAlias.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (aliasTokens.Length == 2)
        {
            var cvTokens = TextNormalizer.Tokenize(text);
            int hits = aliasTokens.Count(t => cvTokens.Contains(t));
            return hits == 2 || (hits == 1 && aliasTokens.Any(t => t.Length >= 8 && cvTokens.Any(c => c.Contains(t))));
        }

        return false;
    }

    private double CalculateSkillMatchRatio(
        HashSet<string> required,
        HashSet<string> candidateSkills,
        List<string> matched,
        List<string> missing)
    {
        if (required.Count == 0) return 0.8; // neutral fallback when JD is unclear

        double points = 0;
        foreach (var skill in required)
        {
            if (candidateSkills.Contains(skill))
            {
                matched.Add(skill);
                points += 1.0;
                continue;
            }

            // Related skill gets partial credit to reduce false penalties
            if (_skillOntology.RelatedSkills.TryGetValue(skill, out var related) &&
                related.Any(r => candidateSkills.Contains(r)))
            {
                matched.Add($"{skill} (related)");
                points += 0.65;
                continue;
            }

            missing.Add(skill);
        }

        return Math.Clamp(points / required.Count, 0, 1);
    }

    private static double CalculateRoleSimilarity(HashSet<string> jobRoles, HashSet<string> cvRoles)
    {
        if (jobRoles.Count == 0) return 0.75; // neutral default
        if (cvRoles.Count == 0) return 0.25;

        int intersection = jobRoles.Intersect(cvRoles, StringComparer.OrdinalIgnoreCase).Count();
        int union = jobRoles.Union(cvRoles, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static double CalculateExperienceScore(double requiredYears, double candidateYears)
    {
        if (requiredYears <= 0) return 0.8; // neutral if JD does not specify
        if (candidateYears <= 0) return 0.15;

        var ratio = candidateYears / requiredYears;
        if (ratio >= 1.15) return 1.0;
        if (ratio >= 1.0) return 0.92;
        if (ratio >= 0.8) return 0.75;
        if (ratio >= 0.6) return 0.55;
        if (ratio >= 0.4) return 0.35;
        return 0.15;
    }

    private static int CalculateConfidenceAdjustment(CandidateProfile candidate, List<string> reasons)
    {
        int adjust = 0;

        if (candidate.WordCount < 120)
        {
            adjust -= 4;
            reasons.Add("Confidence penalty: CV text is very short.");
        }
        else if (candidate.WordCount < 220)
        {
            adjust -= 1;
            reasons.Add("Confidence penalty: CV text is somewhat sparse.");
        }

        if (candidate.CanonicalSkills.Count == 0)
        {
            adjust -= 4;
            reasons.Add("Confidence penalty: no recognizable skills detected.");
        }

        // Slight reward for rich evidence
        if (candidate.CanonicalSkills.Count >= 8 && candidate.WordCount > 260)
        {
            adjust += 3;
            reasons.Add("Confidence boost: CV contains rich, consistent evidence.");
        }

        return adjust;
    }

    private static List<HardFilterEvaluation> EvaluateHardFilters(
        List<string> filters,
        CandidateProfile candidate,
        List<string> reasons)
    {
        var result = new List<HardFilterEvaluation>();
        var cv = $" {TextNormalizer.Normalize(candidate.RawCvText)} ";

        foreach (var filter in filters)
        {
            var lower = TextNormalizer.Normalize(filter);

            // "must work PST" style
            if (lower.Contains("pst"))
            {
                bool explicitlyOtherTimezone = TimezoneRegex().Matches(cv)
                    .Select(m => m.Value)
                    .Any(tz => !tz.Equals("pst", StringComparison.OrdinalIgnoreCase));

                bool mentionsPst = cv.Contains(" pst ", StringComparison.Ordinal);
                if (explicitlyOtherTimezone && !mentionsPst)
                {
                    result.Add(new HardFilterEvaluation(true, $"Hard filter failed: {filter}"));
                    continue;
                }

                result.Add(new HardFilterEvaluation(false, $"Manual check: {filter}"));
                continue;
            }

            // "must be unemployed" style
            if (lower.Contains("unemployed"))
            {
                bool appearsCurrentlyEmployed = CurrentEmploymentRegex().IsMatch(cv);
                if (appearsCurrentlyEmployed)
                {
                    result.Add(new HardFilterEvaluation(true, $"Hard filter failed: {filter}"));
                    continue;
                }

                result.Add(new HardFilterEvaluation(false, $"Manual check: {filter}"));
                continue;
            }

            // Work authorization style
            if (lower.Contains("authorized") || lower.Contains("visa") || lower.Contains("sponsorship"))
            {
                bool needsSponsorship = SponsorshipNeedRegex().IsMatch(cv);
                if (needsSponsorship)
                {
                    result.Add(new HardFilterEvaluation(true, $"Hard filter failed: {filter}"));
                    continue;
                }

                result.Add(new HardFilterEvaluation(false, $"Manual check: {filter}"));
                continue;
            }

            // Default hard filter behavior: flagged for manual verification.
            result.Add(new HardFilterEvaluation(false, $"Manual check: {filter}"));
        }

        if (result.Count > 0 && result.All(x => !x.IsDisqualifying))
            reasons.Add("Hard filters detected: manual verification needed.");

        return result;
    }

    private static HashSet<string> ExtractRoleSignals(string text)
    {
        var normalized = $" {TextNormalizer.Normalize(text)} ";
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var role in RoleSignals)
        {
            if (normalized.Contains($" {role} ", StringComparison.Ordinal))
                roles.Add(role);
        }

        return roles;
    }

    private static double ExtractRequiredYears(string jd)
    {
        var matches = YearsRequiredRegex().Matches(jd);
        if (matches.Count == 0) return 0;

        var values = matches
            .Select(m => m.Groups[1].Value)
            .Select(v => double.TryParse(v, out var n) ? n : 0)
            .Where(v => v > 0)
            .ToList();

        return values.Count == 0 ? 0 : values.Max();
    }

    private static double ExtractCandidateYears(string cv)
    {
        var matches = CandidateYearsRegex().Matches(cv);
        if (matches.Count == 0) return 0;

        var values = matches
            .Select(m => m.Groups[1].Value)
            .Select(v => double.TryParse(v, out var n) ? n : 0)
            .Where(v => v > 0)
            .ToList();

        return values.Count == 0 ? 0 : values.Max();
    }

    private static bool IsCoreHeader(string normalizedLine) =>
        CoreHeaders.Any(h => normalizedLine.StartsWith(h, StringComparison.Ordinal));

    private static bool IsSecondaryHeader(string normalizedLine) =>
        SecondaryHeaders.Any(h => normalizedLine.Contains(h, StringComparison.Ordinal));

    private static bool IsSecondarySignal(string normalizedLine) =>
        SecondarySignals.Any(s => normalizedLine.Contains(s, StringComparison.Ordinal));

    private static bool IsHardFilterLine(string normalizedLine) =>
        HardFilterSignals.Any(s => normalizedLine.Contains(s, StringComparison.Ordinal));

    private static readonly string[] CoreHeaders =
    [
        "requirements", "must have", "required skills", "essential", "qualifications"
    ];

    private static readonly string[] SecondaryHeaders =
    [
        "nice to have", "preferred", "bonus", "a plus", "optional"
    ];

    private static readonly string[] SecondarySignals =
    [
        "preferred", "nice to have", "bonus", "a plus", "desirable"
    ];

    private static readonly string[] HardFilterSignals =
    [
        "must be unemployed", "must work pst", "must be authorized", "visa sponsorship",
        "must reside", "must live", "citizenship required"
    ];

    private static readonly string[] RoleSignals =
    [
        "developer", "engineer", "designer", "architect", "analyst",
        "marketer", "qa", "tester", "devops", "sre",
        "frontend", "backend", "fullstack", "full stack", "data scientist", "data engineer",
        "product manager", "project manager", "wordpress", "ppc"
    ];

    [GeneratedRegex(@"\b(\d+(?:\.\d+)?)\+?\s+years?\s+(?:of\s+)?experience\b", RegexOptions.IgnoreCase)]
    private static partial Regex YearsRequiredRegex();

    [GeneratedRegex(@"\b(\d+(?:\.\d+)?)\+?\s+years?\b", RegexOptions.IgnoreCase)]
    private static partial Regex CandidateYearsRegex();

    [GeneratedRegex(@"\b(pst|est|cst|mst|gmt|utc)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TimezoneRegex();

    [GeneratedRegex(@"\b(present|current|currently)\b", RegexOptions.IgnoreCase)]
    private static partial Regex CurrentEmploymentRegex();

    [GeneratedRegex(@"\b(need|require)\s+(visa|sponsorship)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SponsorshipNeedRegex();

    private enum Tier { Core, Secondary }

    private sealed record ParsedJobProfile(
        HashSet<string> CoreSkills,
        HashSet<string> NiceToHaveSkills,
        List<string> HardFilters,
        double RequiredYears,
        HashSet<string> RoleSignals
    );

    private sealed record CandidateProfile(
        HashSet<string> CanonicalSkills,
        double YearsExperience,
        HashSet<string> RoleSignals,
        int WordCount,
        string RawCvText
    );

    private sealed record HardFilterEvaluation(bool IsDisqualifying, string Reason);
}

