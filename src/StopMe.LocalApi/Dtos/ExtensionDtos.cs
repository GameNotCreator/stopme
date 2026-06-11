namespace StopMe.LocalApi.Dtos;

public sealed class ExtensionHeartbeatRequest
{
    public required string Browser { get; init; }
    public required string ExtensionId { get; init; }
    public string? ExtensionVersion { get; init; }
}

public sealed class ExtensionHealthDto
{
    public required IReadOnlyList<ExtensionBrowserHealthDto> Browsers { get; init; }
    public required int StaleThresholdSeconds { get; init; }
}

public sealed class ExtensionBrowserHealthDto
{
    public required string Browser { get; init; }
    public required string ExtensionId { get; init; }
    public string? ExtensionVersion { get; init; }
    public required DateTime LastHeartbeatUtc { get; init; }
    public required int SecondsSinceLastHeartbeat { get; init; }
    public required bool IsStale { get; init; }
}

public sealed class BypassEventDto
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }
    public required DateTime DetectedAtUtc { get; init; }
    public Guid? SessionId { get; init; }
    public string? Source { get; init; }
    public string? Detail { get; init; }
}

public sealed class BypassEventsResponse
{
    public required IReadOnlyList<BypassEventDto> Events { get; init; }
}

public sealed class RuleSnapshotResponse
{
    public required DateTime ComputedAtUtc { get; init; }
    public required IReadOnlyList<CompiledRuleDto> Rules { get; init; }
}

public sealed class CompiledRuleDto
{
    public required Guid RuleId { get; init; }
    public required Guid BlocklistId { get; init; }
    public required string Type { get; init; }
    public required string Value { get; init; }
    public required string SourceMode { get; init; }
    public Guid? SessionId { get; init; }
    public string? SessionName { get; init; }
    public DateTime? SessionEndUtc { get; init; }
}
