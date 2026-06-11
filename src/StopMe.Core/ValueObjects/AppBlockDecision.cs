using StopMe.Core.Enums;

namespace StopMe.Core.ValueObjects;

public sealed class AppBlockDecision
{
    public bool IsBlocked { get; init; }

    public string? Reason { get; init; }

    public Guid? MatchedRuleId { get; init; }

    public RuleType? MatchedRuleType { get; init; }

    public Guid? SessionId { get; init; }

    public string? SessionName { get; init; }

    public static AppBlockDecision Allow() => new() { IsBlocked = false };

    public static AppBlockDecision Block(string reason, Guid? ruleId = null, RuleType? ruleType = null, Guid? sessionId = null, string? sessionName = null) =>
        new()
        {
            IsBlocked = true,
            Reason = reason,
            MatchedRuleId = ruleId,
            MatchedRuleType = ruleType,
            SessionId = sessionId,
            SessionName = sessionName,
        };
}
