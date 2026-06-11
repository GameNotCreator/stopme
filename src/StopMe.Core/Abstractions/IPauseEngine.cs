using StopMe.Core.Enums;

namespace StopMe.Core.Abstractions;

// Phase 8 contract. The default implementation (NullPauseEngine) treats every
// session as "no pause". A future Pomodoro/Allowance implementation plugs in
// here without changing SessionEngine.
//
// The contract is: given a session's pause policy and an instantaneous time,
// is the session currently in a "focus" window or a "break" window?
public interface IPauseEngine
{
    PauseState Evaluate(PauseEvaluationInput input);
}

public sealed class PauseEvaluationInput
{
    public required Guid SessionId { get; init; }
    public required DateTime SessionStartUtc { get; init; }
    public required DateTime NowUtc { get; init; }
    public required PauseType Type { get; init; }
    public int? FocusMinutes { get; init; }
    public int? BreakMinutes { get; init; }
    public int? CyclesBeforeLongBreak { get; init; }
    public int? LongBreakMinutes { get; init; }
    public int? DailyAllowanceMinutes { get; init; }
}

public enum PausePhase
{
    Focus,
    Break,
}

public sealed class PauseState
{
    public required PausePhase Phase { get; init; }
    public required TimeSpan TimeIntoPhase { get; init; }
    public required TimeSpan PhaseDuration { get; init; }
    public TimeSpan Remaining => PhaseDuration - TimeIntoPhase;
}
