using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Resume.Api.Configuration;

namespace Resume.Api.Services.Scoring;

public sealed class SkillOntology
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public IReadOnlyDictionary<string, string[]> CanonicalSkills { get; }
    public IReadOnlyDictionary<string, string[]> RelatedSkills { get; }
    public IEnumerable<string> AllCanonicals => CanonicalSkills.Keys;

    public SkillOntology(IWebHostEnvironment env)
    {
        var path = Path.Combine(env.ContentRootPath, "Configuration", "skills.json");
        if (!File.Exists(path))
            throw new InvalidOperationException($"Skill configuration not found at '{path}'.");

        var json = File.ReadAllText(path);
        var dto = JsonSerializer.Deserialize<SkillsFileDto>(json, SerializerOptions)
            ?? throw new InvalidOperationException("skills.json could not be parsed.");

        if (dto.Skills.Count == 0)
            throw new InvalidOperationException("skills.json must define at least one skill.");

        var canonical = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in dto.Skills)
        {
            if (string.IsNullOrWhiteSpace(s.Canonical))
                throw new InvalidOperationException("Each skill must have a non-empty Canonical.");
            if (canonical.ContainsKey(s.Canonical))
                throw new InvalidOperationException($"Duplicate canonical skill: {s.Canonical}");
            canonical[s.Canonical] = [.. s.Aliases ?? []];
        }

        CanonicalSkills = canonical;

        var related = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in dto.RelatedSkills)
            related[kv.Key] = [.. kv.Value ?? []];
        RelatedSkills = related;
    }

    /// <summary>Returns the canonical skill key when <paramref name="alias"/> matches a configured alias (case-insensitive).</summary>
    public bool TryGetCanonical(string alias, [NotNullWhen(true)] out string? canonical)
    {
        foreach (var kv in CanonicalSkills)
        {
            foreach (var a in kv.Value)
            {
                if (string.Equals(a, alias, StringComparison.OrdinalIgnoreCase))
                {
                    canonical = kv.Key;
                    return true;
                }
            }
        }

        canonical = null;
        return false;
    }
}
