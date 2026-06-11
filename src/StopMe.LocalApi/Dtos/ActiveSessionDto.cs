namespace StopMe.LocalApi.Dtos;

public sealed class ActiveSessionDto
{
    public required Guid SessionId { get; init; }
    public required string Name { get; init; }
    public required DateTime StartAtUtc { get; init; }
    public required DateTime EndAtUtc { get; init; }
    public required string Mode { get; init; }            // SessionMode.ToString()
    public required string LockPolicy { get; init; }      // LockPolicy.ToString()
    public required int RemainingSeconds { get; init; }
    public required IReadOnlyList<Guid> BlocklistIds { get; init; }

    // null when the session has no pause policy. "Focus" / "Break" otherwise.
    public string? PausePhase { get; init; }
    public int PausePhaseRemainingSeconds { get; init; }
}
