using System.Text.RegularExpressions;

namespace Resume.Api.Services.Scoring;

public static partial class TextNormalizer
{
    public static string Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var text = input.ToLowerInvariant();
        text = NonAlphaNumericRegex().Replace(text, " ");
        text = MultiSpaceRegex().Replace(text, " ").Trim();
        return text;
    }

    public static HashSet<string> Tokenize(string input)
    {
        var normalized = Normalize(input);
        if (string.IsNullOrWhiteSpace(normalized))
            return [];

        return normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public static bool ContainsNormalizedPhrase(string haystack, string needle)
    {
        var h = $" {Normalize(haystack)} ";
        var n = $" {Normalize(needle)} ";
        return h.Contains(n, StringComparison.Ordinal);
    }

    public static bool FuzzyContainsToken(string haystack, string token, double threshold = 0.84)
    {
        var hayTokens = Tokenize(haystack);
        var normalizedNeedle = Normalize(token);
        if (string.IsNullOrWhiteSpace(normalizedNeedle)) return false;

        foreach (var t in hayTokens)
        {
            if (t == normalizedNeedle) return true;
            if (t.Length < 3 || normalizedNeedle.Length < 3) continue;
            var sim = Similarity(t, normalizedNeedle);
            if (sim >= threshold) return true;
        }
        return false;
    }

    public static double Similarity(string a, string b)
    {
        if (a == b) return 1;
        if (a.Length == 0 || b.Length == 0) return 0;

        int distance = Levenshtein(a, b);
        int maxLen = Math.Max(a.Length, b.Length);
        return 1d - (double)distance / maxLen;
    }

    private static int Levenshtein(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost
                );
            }
        }
        return dp[a.Length, b.Length];
    }

    [GeneratedRegex(@"[^a-z0-9\+\#]+")]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex MultiSpaceRegex();
}

