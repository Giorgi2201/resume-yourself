using System.Text.RegularExpressions;

namespace Resume.Api.Services.NameExtraction;

public partial class NameExtractionService(ILogger<NameExtractionService> logger) : INameExtractionService
{
    private const double MinAcceptedConfidence = 0.55;

    private static readonly HashSet<string> SectionWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "experience", "skills", "summary", "profile", "education", "projects", "certifications",
        "contact", "objective", "references", "languages", "interests", "achievements",
        "company"
    };

    private static readonly HashSet<string> JobTitleWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "developer", "engineer", "manager", "designer", "architect", "analyst", "consultant",
        "specialist", "director", "intern", "lead", "senior", "junior", "frontend", "backend",
        "fullstack", "full-stack", "software", "product", "marketing", "sales", "hr", "dev"
    };

    private static readonly HashSet<string> SkillWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "javascript", "typescript", "python", "java", "angular", "react", "vue",
        "html", "css", "node", "sql", "aws", "azure", "gcp", "docker", "kubernetes",
        "wordpress", "figma", "jira", "git", "rest", "graphql"
    };

    // Name particles that may appear lowercase in valid names.
    private static readonly HashSet<string> NameParticles = new(StringComparer.OrdinalIgnoreCase)
    {
        "de", "del", "der", "van", "von", "da", "dos", "das", "bin", "binti",
        "al", "el", "ibn", "la", "le", "du", "des"
    };

    private static readonly HashSet<string> LinkedinNoiseSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "dev", "developer", "engineer", "official", "profile", "cv", "resume"
    };

    public NameExtractionResult Extract(string rawText, string? email = null, string? fileName = null)
    {
        var lines = SplitLines(rawText);
        var normalizedCounts = BuildLineFrequency(lines);

        var candidates = new List<NameCandidate>();

        // Strategy 1: Header/top region.
        candidates.AddRange(ExtractFromHeader(lines, normalizedCounts));

        // Strategy 2: Labeled fields.
        candidates.AddRange(ExtractFromLabeledFields(lines, normalizedCounts));

        // Strategy 3: Email.
        var resolvedEmail = string.IsNullOrWhiteSpace(email) ? ExtractFirstEmail(rawText) : email!;
        if (!string.IsNullOrWhiteSpace(resolvedEmail))
        {
            var fromEmail = ExtractFromEmail(resolvedEmail!, normalizedCounts);
            if (fromEmail is not null) candidates.Add(fromEmail);
        }

        // Strategy 4: LinkedIn URL.
        candidates.AddRange(ExtractFromLinkedIn(rawText, normalizedCounts));

        // Strategy 5: Heuristic fallback around contact and top section.
        candidates.AddRange(ExtractHeuristicFallback(lines, normalizedCounts, resolvedEmail));

        // Optional file-name clue as fallback signal only.
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var fromFileName = ExtractFromFileName(fileName!, normalizedCounts);
            if (fromFileName is not null) candidates.Add(fromFileName);
        }

        var best = candidates
            .Where(c => IsLikelyName(c.FullName, strict: true))
            .OrderByDescending(c => c.Confidence)
            .ThenBy(c => c.Source)
            .FirstOrDefault();

        if (best is null || best.Confidence < MinAcceptedConfidence)
        {
            logger.LogWarning(
                "No name could be extracted (best confidence={Confidence:F2}, threshold={Threshold:F2})",
                best?.Confidence ?? 0, MinAcceptedConfidence);
            return new NameExtractionResult(
                FirstName: string.Empty,
                LastName: string.Empty,
                FullName: "Unknown",
                ConfidenceScore: 0,
                Source: "none"
            );
        }

        var normalized = NormalizeName(best.FullName);
        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var first = parts.Length > 0 ? parts[0] : string.Empty;
        var last = parts.Length > 1 ? parts[^1] : string.Empty;

        logger.LogDebug(
            "Name extracted: '{Name}' via {Source} (confidence={Confidence:F2})",
            normalized, best.Source, best.Confidence);

        return new NameExtractionResult(
            FirstName: first,
            LastName: last,
            FullName: normalized,
            ConfidenceScore: Math.Round(best.Confidence, 3),
            Source: best.Source
        );
    }

    private static List<NameCandidate> ExtractFromHeader(
        List<string> lines,
        Dictionary<string, int> lineFrequency)
    {
        var candidates = new List<NameCandidate>();
        var top = lines.Take(15).ToList();

        for (int i = 0; i < top.Count; i++)
        {
            var line = top[i];
            if (!IsLikelyName(line, strict: false)) continue;

            // Strong confidence for very top area, then decays.
            double confidence = 0.78 - (i * 0.025);
            confidence += ContextBoost(line, lineFrequency);
            candidates.Add(new NameCandidate(line, Clamp01(confidence), "header"));
        }

        return candidates;
    }

    private static List<NameCandidate> ExtractFromLabeledFields(
        List<string> lines,
        Dictionary<string, int> lineFrequency)
    {
        var list = new List<NameCandidate>();
        foreach (var line in lines.Take(60))
        {
            var match = NameLabelRegex().Match(line);
            if (!match.Success) continue;

            var value = match.Groups[1].Value.Trim();
            if (!IsLikelyName(value, strict: true)) continue;

            double confidence = 0.92 + ContextBoost(value, lineFrequency);
            list.Add(new NameCandidate(value, Clamp01(confidence), "labeled-field"));
        }
        return list;
    }

    private static NameCandidate? ExtractFromEmail(string email, Dictionary<string, int> lineFrequency)
    {
        var local = email.Split('@')[0];
        if (local.Any(char.IsDigit)) return null;
        var clean = Regex.Replace(local, @"[^a-zA-Z\._\-]", "");
        var parts = clean.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length >= 2)
            .ToList();

        if (parts.Count < 2) return null;

        // Ignore noisy tokens with many digits.
        if (parts.Any(p => p.Any(char.IsDigit))) return null;

        var candidate = string.Join(" ", parts.Take(3).Select(ToTitleWord));
        if (!IsLikelyName(candidate, strict: true)) return null;

        double confidence = 0.73 + ContextBoost(candidate, lineFrequency);
        return new NameCandidate(candidate, Clamp01(confidence), "email");
    }

    private static List<NameCandidate> ExtractFromLinkedIn(string text, Dictionary<string, int> lineFrequency)
    {
        var results = new List<NameCandidate>();

        foreach (Match m in LinkedinRegex().Matches(text))
        {
            if (!m.Success) continue;
            var slug = m.Groups[1].Value;
            if (string.IsNullOrWhiteSpace(slug)) continue;

            var parts = slug.Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
                .Where(p => p.Length >= 2 && p.All(char.IsLetter))
                .ToList();

            while (parts.Count > 2 && LinkedinNoiseSuffixes.Contains(parts[^1]))
                parts.RemoveAt(parts.Count - 1);

            if (parts.Count < 2) continue;

            var candidate = string.Join(" ", parts.Take(3).Select(ToTitleWord));
            if (!IsLikelyName(candidate, strict: true)) continue;

            double confidence = 0.76 + ContextBoost(candidate, lineFrequency);
            results.Add(new NameCandidate(candidate, Clamp01(confidence), "linkedin"));
        }

        return results;
    }

    private static List<NameCandidate> ExtractHeuristicFallback(
        List<string> lines,
        Dictionary<string, int> lineFrequency,
        string? email)
    {
        var list = new List<NameCandidate>();
        var top = lines.Take(35).ToList();

        // Region near email/phone is often where real name appears.
        int pivot = -1;
        for (int i = 0; i < top.Count; i++)
        {
            var l = top[i].ToLowerInvariant();
            if ((!string.IsNullOrWhiteSpace(email) && l.Contains(email!.ToLowerInvariant(), StringComparison.Ordinal))
                || EmailRegex().IsMatch(top[i])
                || PhoneRegex().IsMatch(top[i]))
            {
                pivot = i;
                break;
            }
        }

        var candidateIndices = pivot >= 0
            ? Enumerable.Range(Math.Max(0, pivot - 4), Math.Min(top.Count - Math.Max(0, pivot - 4), 9))
            : Enumerable.Range(0, Math.Min(top.Count, 15));

        foreach (var idx in candidateIndices)
        {
            var line = top[idx];
            if (!IsLikelyName(line, strict: false)) continue;

            double confidence = 0.58;
            if (pivot >= 0 && Math.Abs(idx - pivot) <= 2) confidence += 0.08;
            confidence += ContextBoost(line, lineFrequency);

            list.Add(new NameCandidate(line, Clamp01(confidence), "heuristic"));
        }

        return list;
    }

    private static NameCandidate? ExtractFromFileName(string fileName, Dictionary<string, int> lineFrequency)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var clean = Regex.Replace(stem, @"(?i)\b(cv|resume|curriculum|vitae|profile|updated|final)\b", " ");
        clean = Regex.Replace(clean, @"[_\-.]+", " ");
        clean = Regex.Replace(clean, @"\s+", " ").Trim();

        var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2 && w.All(char.IsLetter))
            .ToList();

        if (words.Count is < 2 or > 3) return null;

        var candidate = string.Join(" ", words.Select(ToTitleWord));
        if (!IsLikelyName(candidate, strict: true)) return null;

        double confidence = 0.52 + ContextBoost(candidate, lineFrequency);
        return new NameCandidate(candidate, Clamp01(confidence), "filename");
    }

    private static bool IsLikelyName(string input, bool strict)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        var name = input.Trim();
        if (name.Length < 2 || name.Length > 45) return false;
        if (name.Count(char.IsLetter) < 2) return false;
        if (name.Contains("http", StringComparison.OrdinalIgnoreCase) || name.Contains('@')) return false;

        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 1 || words.Length > 3) return false;

        // Reject long sentence-like lines.
        if (name.Contains(',') || name.Contains(';') || name.Contains('|') || name.Contains('/'))
            return false;

        foreach (var raw in words)
        {
            var w = raw.Trim();
            if (!NameWordRegex().IsMatch(w)) return false;
            if (w.Length < 2) return false;

            if (SectionWords.Contains(w) || JobTitleWords.Contains(w) || SkillWords.Contains(w))
                return false;
        }

        // Reject all-uppercase blocks unless short and structurally valid.
        if (name == name.ToUpperInvariant() && strict)
        {
            // only allow if 2 words and each looks like a proper token
            if (words.Length != 2) return false;
        }

        // Require probable proper case for strict mode, except particles.
        if (strict)
        {
            var properCaseHits = words.Count(w =>
                NameParticles.Contains(w) ||
                (char.IsUpper(w[0]) && w.Skip(1).All(c => char.IsLower(c) || c == '\'' || c == '-')));

            if (properCaseHits < Math.Max(1, words.Length - 1))
                return false;
        }

        return true;
    }

    private static double ContextBoost(string candidateName, Dictionary<string, int> lineFrequency)
    {
        var normalized = NormalizeKey(candidateName);
        var repeats = lineFrequency.TryGetValue(normalized, out var count) ? count : 1;

        // repeated lines are usually headers duplicated by parser artifacts
        if (repeats > 2) return -0.18;
        if (repeats == 2) return -0.08;
        return 0.03;
    }

    private static List<string> SplitLines(string text)
    {
        return text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct() // remove exact duplicates early
            .ToList();
    }

    private static Dictionary<string, int> BuildLineFrequency(IEnumerable<string> lines)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var key = NormalizeKey(line);
            if (string.IsNullOrWhiteSpace(key)) continue;
            map[key] = map.TryGetValue(key, out var c) ? c + 1 : 1;
        }
        return map;
    }

    private static string NormalizeKey(string input)
        => Regex.Replace(input.ToLowerInvariant(), @"\s+", " ").Trim();

    private static string ExtractFirstEmail(string text)
    {
        var m = EmailRegex().Match(text);
        return m.Success ? m.Value : string.Empty;
    }

    private static string NormalizeName(string name)
    {
        return string.Join(" ",
            name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(w => NameParticles.Contains(w) ? w.ToLowerInvariant() : ToTitleWord(w)));
    }

    private static string ToTitleWord(string w)
    {
        if (string.IsNullOrWhiteSpace(w)) return w;
        var parts = w.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var ap = part.Split('\'', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p.Length switch
                    {
                        0 => p,
                        1 => p.ToUpperInvariant(),
                        _ => char.ToUpper(p[0]) + p[1..].ToLowerInvariant()
                    });
                return string.Join("'", ap);
            });
        return string.Join("-", parts);
    }

    private static double Clamp01(double v) => Math.Max(0, Math.Min(1, v));

    private sealed record NameCandidate(string FullName, double Confidence, string Source);

    [GeneratedRegex(@"(?im)^(?:full\s+)?(?:candidate\s+)?name\s*:\s*(.+)$")]
    private static partial Regex NameLabelRegex();

    [GeneratedRegex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?i)linkedin\.com\/in\/([a-z0-9\-_]+)")]
    private static partial Regex LinkedinRegex();

    [GeneratedRegex(@"^\+?[\d\-\s\(\)]{7,20}$")]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^[\p{L}][\p{L}'\-]{0,30}$")]
    private static partial Regex NameWordRegex();
}

