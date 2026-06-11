using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Engines;

public static class UrlMatcher
{
    private static readonly ConcurrentDictionary<string, Regex> RegexCache = new(StringComparer.Ordinal);

    public static bool MatchesDomain(string ruleValue, NormalizedUrl url)
    {
        var rule = StripWww(ruleValue.Trim().ToLowerInvariant().TrimEnd('/'));
        if (rule.Length == 0) return false;

        var host = StripWww(url.Host);
        if (string.Equals(host, rule, StringComparison.Ordinal)) return true;
        return host.EndsWith("." + rule, StringComparison.Ordinal);
    }

    public static bool MatchesExactUrl(string ruleValue, NormalizedUrl url)
    {
        if (!UrlNormalizer.TryNormalize(ruleValue, out var ruleUrl) || ruleUrl is null)
            return false;

        var ruleHost = StripWww(ruleUrl.Host);
        var urlHost = StripWww(url.Host);
        if (!string.Equals(ruleHost, urlHost, StringComparison.Ordinal)
            && !urlHost.EndsWith("." + ruleHost, StringComparison.Ordinal))
            return false;

        var rulePath = ruleUrl.Path.TrimEnd('/');
        var urlPath = url.Path.TrimEnd('/');

        if (string.IsNullOrEmpty(ruleUrl.Query))
        {
            // Path-prefix match (host/path semantics)
            return rulePath.Length == 0
                || string.Equals(rulePath, urlPath, StringComparison.Ordinal)
                || urlPath.StartsWith(rulePath + "/", StringComparison.Ordinal);
        }

        if (!string.Equals(rulePath, urlPath, StringComparison.Ordinal))
            return false;

        return string.Equals(ruleUrl.Query, url.Query ?? string.Empty, StringComparison.Ordinal);
    }

    public static bool MatchesWildcard(string ruleValue, NormalizedUrl url)
    {
        var pattern = ruleValue.Trim().ToLowerInvariant();
        if (pattern.Length == 0) return false;

        // Treat ** identically to *
        while (pattern.Contains("**", StringComparison.Ordinal))
            pattern = pattern.Replace("**", "*", StringComparison.Ordinal);

        if (pattern == "*" || pattern == "*.*")
            return true;

        var target = url.HostPathQuery;

        if (Matches(pattern, target)) return true;

        // Apex-tolerant: "*.example.com" should also match "example.com"
        if (pattern.StartsWith("*.", StringComparison.Ordinal))
        {
            var apex = pattern[2..];
            if (Matches(apex, target)) return true;
        }

        // Host-only patterns (no slash, no '?') should be allowed to match host alone
        if (!pattern.Contains('/', StringComparison.Ordinal) && !pattern.Contains('?', StringComparison.Ordinal))
        {
            if (Matches(pattern, url.Host)) return true;
            if (pattern.StartsWith("*.", StringComparison.Ordinal) && Matches(pattern[2..], url.Host)) return true;
        }

        return false;
    }

    public static bool MatchesAnyAllowable(CompiledRule rule, NormalizedUrl url) =>
        rule.Type switch
        {
            RuleType.Domain or RuleType.ExceptionDomain => MatchesDomain(rule.Value, url),
            RuleType.Url or RuleType.ExceptionUrl => MatchesExactUrl(rule.Value, url),
            RuleType.Wildcard or RuleType.ExceptionWildcard => MatchesWildcard(rule.Value, url),
            _ => false,
        };

    private static bool Matches(string pattern, string target)
    {
        var regex = RegexCache.GetOrAdd(pattern, GlobToRegex);
        return regex.IsMatch(target);
    }

    private static Regex GlobToRegex(string glob)
    {
        var sb = new StringBuilder("^");
        foreach (var c in glob)
        {
            if (c == '*')
            {
                sb.Append(".*");
            }
            else if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
            {
                sb.Append(c);
            }
            else
            {
                sb.Append('\\').Append(c);
            }
        }
        sb.Append('$');
        return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    }

    private static string StripWww(string host)
    {
        const string prefix = "www.";
        return host.StartsWith(prefix, StringComparison.Ordinal) ? host[prefix.Length..] : host;
    }
}
