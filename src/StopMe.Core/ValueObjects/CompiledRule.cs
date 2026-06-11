using StopMe.Core.Enums;

namespace StopMe.Core.ValueObjects;

public sealed class CompiledRule
{
    public required Guid RuleId { get; init; }

    public required Guid BlocklistId { get; init; }

    public required RuleType Type { get; init; }

    public required string Value { get; init; }

    public required BlocklistMode SourceMode { get; init; }

    public Guid? SessionId { get; init; }

    public string? SessionName { get; init; }

    public DateTime? SessionEndUtc { get; init; }
}
