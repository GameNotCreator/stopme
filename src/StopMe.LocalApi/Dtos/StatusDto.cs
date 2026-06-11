namespace StopMe.LocalApi.Dtos;

public sealed class StatusDto
{
    public required string ServiceVersion { get; init; }
    public required DateTime NowUtc { get; init; }
    public required DateTime LastTickUtc { get; init; }
    public required bool BootRestoreCompleted { get; init; }
    public required int ActiveSessionCount { get; init; }
    public required int CompiledRuleCount { get; init; }
}
