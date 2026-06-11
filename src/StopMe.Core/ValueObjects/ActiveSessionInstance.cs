using StopMe.Core.Abstractions;
using StopMe.Core.Enums;

namespace StopMe.Core.ValueObjects;

public sealed class ActiveSessionInstance
{
    public required Guid SessionId { get; init; }

    public required string Name { get; init; }

    public required DateTime StartAtUtc { get; init; }

    public required DateTime EndAtUtc { get; init; }

    public required SessionMode Mode { get; init; }

    public required LockPolicy LockPolicy { get; init; }

    public required IReadOnlyList<Guid> BlocklistIds { get; init; }

    // Set when a PausePolicy applies (Pomodoro for now). null = no pause policy
    // applies — caller treats it as permanent Focus.
    public PausePhase? PausePhase { get; init; }

    // Seconds left in the current pause phase. 0 when no pause policy applies.
    public int PausePhaseRemainingSeconds { get; init; }

    public int RemainingSeconds(DateTime nowUtc)
    {
        var remaining = EndAtUtc - nowUtc;
        return remaining > TimeSpan.Zero ? (int)remaining.TotalSeconds : 0;
    }

    public bool IsOnBreak => PausePhase == Abstractions.PausePhase.Break;
}
