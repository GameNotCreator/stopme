using StopMe.Core.ValueObjects;

namespace StopMe.Service.State;

public sealed record RuleSnapshot(
    DateTime ComputedAtUtc,
    IReadOnlyList<ActiveSessionInstance> ActiveSessions,
    IReadOnlyList<CompiledRule> CompiledRules)
{
    public static readonly RuleSnapshot Empty = new(
        ComputedAtUtc: DateTime.MinValue,
        ActiveSessions: Array.Empty<ActiveSessionInstance>(),
        CompiledRules: Array.Empty<CompiledRule>());
}
