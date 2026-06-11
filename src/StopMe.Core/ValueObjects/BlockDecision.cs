using StopMe.Core.Enums;

namespace StopMe.Core.ValueObjects;

public sealed class BlockDecision
{
    public bool IsBlocked { get; init; }

    public string? Reason { get; init; }

    public Guid? MatchedRuleId { get; init; }

    public RuleType? MatchedRuleType { get; init; }

    public Guid? SessionId { get; init; }

    public string? SessionName { get; init; }

    public int? RemainingSeconds { get; init; }

    public string? RedirectBlockPageUrl { get; init; }

    public static BlockDecision Allow() => new() { IsBlocked = false };

    public static BlockDecision Block(string reason, Guid? ruleId = null, RuleType? ruleType = null, Guid? sessionId = null, string? sessionName = null, int? remainingSeconds = null) =>
        new()
        {
            IsBlocked = true,
            Reason = reason,
            MatchedRuleId = ruleId,
            MatchedRuleType = ruleType,
            SessionId = sessionId,
            SessionName = sessionName,
            RemainingSeconds = remainingSeconds,
        };
}
