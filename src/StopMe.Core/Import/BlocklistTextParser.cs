using StopMe.Core.Enums;
using StopMe.Core.Models;

namespace StopMe.Core.Import;

public sealed class ParsedRule
{
    public required RuleType Type { get; init; }
    public required string Value { get; init; }
    public string? Notes { get; init; }
}

public static class BlocklistTextParser
{
    public static IReadOnlyList<ParsedRule> Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var rules = new List<ParsedRule>();
        var lineNumber = 0;

        foreach (var rawLine in content.Split('\n'))
        {
            lineNumber++;
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0) continue;

            var (type, value) = ClassifyLine(line);
            rules.Add(new ParsedRule { Type = type, Value = value });
        }

        return rules;
    }

    public static IReadOnlyList<BlockRule> ParseToBlockRules(string content, Guid blocklistId, DateTime createdAtUtc)
    {
        var parsed = Parse(content);
        return parsed.Select(p => new BlockRule
        {
            Id = Guid.NewGuid(),
            BlocklistId = blocklistId,
            Type = p.Type,
            Value = p.Value,
            IsEnabled = true,
            Notes = p.Notes,
            CreatedAtUtc = createdAtUtc,
        }).ToArray();
    }

    public static string Format(IEnumerable<BlockRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return string.Join('\n', rules.Where(r => r.IsEnabled).Select(FormatRule));
    }

    private static string FormatRule(BlockRule rule) =>
        rule.Type switch
        {
            RuleType.ExceptionUrl => "allow:" + rule.Value,
            RuleType.ExceptionDomain => "allow:" + rule.Value,
            RuleType.ExceptionWildcard => "allow:" + rule.Value,
            RuleType.Category => "category:" + rule.Value,
            RuleType.FullInternet => "*.*",
            _ => rule.Value,
        };

    private static string StripComment(string line)
    {
        var idx = line.IndexOfAny(new[] { '#', '!' });
        return idx >= 0 ? line[..idx] : line;
    }

    private static (RuleType type, string value) ClassifyLine(string line)
    {
        if (line.StartsWith("allow:", StringComparison.OrdinalIgnoreCase))
        {
            var value = line["allow:".Length..].Trim();
            return ClassifyAllow(value);
        }

        if (line.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
        {
            var value = line["category:".Length..].Trim();
            return (RuleType.Category, value);
        }

        if (line == "*.*" || line == "*")
            return (RuleType.FullInternet, "*");

        if (line.Contains('*', StringComparison.Ordinal))
            return (RuleType.Wildcard, line);

        if (line.Contains('/', StringComparison.Ordinal))
            return (RuleType.Url, line);

        return (RuleType.Domain, line);
    }

    private static (RuleType type, string value) ClassifyAllow(string value)
    {
        if (value.Contains('*', StringComparison.Ordinal))
            return (RuleType.ExceptionWildcard, value);
        if (value.Contains('/', StringComparison.Ordinal))
            return (RuleType.ExceptionUrl, value);
        return (RuleType.ExceptionDomain, value);
    }
}
