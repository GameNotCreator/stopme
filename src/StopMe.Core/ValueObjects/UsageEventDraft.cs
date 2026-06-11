using StopMe.Core.Enums;

namespace StopMe.Core.ValueObjects;

// What a caller wants to record — before privacy filtering.
// The recorder decides what to keep, hash, or drop based on the active
// StatsRetentionPolicy at write time.
public sealed class UsageEventDraft
{
    public required UsageEventType Type { get; init; }
    public Guid? SessionId { get; init; }
    public Guid? RuleId { get; init; }
    public string? DomainLabel { get; init; }
    public string? AppNameLabel { get; init; }
    public string? DisplayLabel { get; init; }
}
