using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Engines;

public sealed class UrlRuleEngine : IUrlRuleEngine
{
    private readonly TimeProvider _clock;

    public UrlRuleEngine() : this(TimeProvider.System) { }

    public UrlRuleEngine(TimeProvider clock) => _clock = clock;

    public BlockDecision Evaluate(string rawUrl, IReadOnlyList<CompiledRule> activeRules)
    {
        ArgumentNullException.ThrowIfNull(activeRules);

        if (!UrlNormalizer.TryNormalize(rawUrl, out var url) || url is null)
            return BlockDecision.Allow();

        // Priority 1–3: exceptions always win.
        foreach (var rule in activeRules)
        {
            if (rule.Type == RuleType.ExceptionUrl && UrlMatcher.MatchesExactUrl(rule.Value, url))
                return BlockDecision.Allow();
            if (rule.Type == RuleType.ExceptionDomain && UrlMatcher.MatchesDomain(rule.Value, url))
                return BlockDecision.Allow();
            if (rule.Type == RuleType.ExceptionWildcard && UrlMatcher.MatchesWildcard(rule.Value, url))
                return BlockDecision.Allow();
        }

        // Priority 4: every Allowlist-mode blocklist must contain the URL.
        var allowlistGroups = activeRules
            .Where(r => r.SourceMode == BlocklistMode.Allowlist)
            .GroupBy(r => r.BlocklistId);

        foreach (var group in allowlistGroups)
        {
            bool included = group.Any(r => UrlMatcher.MatchesAnyAllowable(r, url));
            if (!included)
            {
                var anchor = group.First();
                return BlockDecision.Block(
                    reason: BuildReason(anchor, "not in allowlist"),
                    ruleId: anchor.RuleId,
                    ruleType: anchor.Type,
                    sessionId: anchor.SessionId,
                    sessionName: anchor.SessionName,
                    remainingSeconds: RemainingSeconds(anchor));
            }
        }

        // Priority 5: FullInternet rule type OR a blocklist whose Mode is FullInternet.
        var fullInternet = activeRules.FirstOrDefault(r =>
            r.Type == RuleType.FullInternet || r.SourceMode == BlocklistMode.FullInternet);
        if (fullInternet is not null)
        {
            return BlockDecision.Block(
                reason: BuildReason(fullInternet, "full Internet blocked"),
                ruleId: fullInternet.RuleId,
                ruleType: RuleType.FullInternet,
                sessionId: fullInternet.SessionId,
                sessionName: fullInternet.SessionName,
                remainingSeconds: RemainingSeconds(fullInternet));
        }

        // Priority 6-9: only Blacklist-mode rules act as block rules. Allowlist-mode rules
        // have already had their say in step 4; FullInternet-mode rules in step 5.
        var blockRules = activeRules.Where(r => r.SourceMode == BlocklistMode.Blacklist);

        foreach (var rule in blockRules.Where(r => r.Type == RuleType.Url))
        {
            if (UrlMatcher.MatchesExactUrl(rule.Value, url))
                return BlockResult(rule, "URL match");
        }

        foreach (var rule in blockRules.Where(r => r.Type == RuleType.Domain))
        {
            if (UrlMatcher.MatchesDomain(rule.Value, url))
                return BlockResult(rule, "domain match");
        }

        // Wildcard (and Category, which the compiler pre-expands to Domain).
        foreach (var rule in blockRules.Where(r => r.Type == RuleType.Wildcard))
        {
            if (UrlMatcher.MatchesWildcard(rule.Value, url))
                return BlockResult(rule, "wildcard match");
        }

        return BlockDecision.Allow();
    }

    private BlockDecision BlockResult(CompiledRule rule, string what) =>
        BlockDecision.Block(
            reason: BuildReason(rule, what),
            ruleId: rule.RuleId,
            ruleType: rule.Type,
            sessionId: rule.SessionId,
            sessionName: rule.SessionName,
            remainingSeconds: RemainingSeconds(rule));

    private static string BuildReason(CompiledRule rule, string what) =>
        rule.SessionName is null
            ? $"Blocked by {what} ({rule.Value})"
            : $"Blocked by {what} during '{rule.SessionName}'";

    private int? RemainingSeconds(CompiledRule rule)
    {
        if (rule.SessionEndUtc is null) return null;
        var remaining = rule.SessionEndUtc.Value - _clock.GetUtcNow().UtcDateTime;
        return remaining > TimeSpan.Zero ? (int)remaining.TotalSeconds : 0;
    }
}
