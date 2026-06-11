using StopMe.Core.Abstractions;
using StopMe.Core.Enums;

namespace StopMe.Core.Engines;

// Default Phase 8 implementation: every session is permanently in Focus mode.
// Replaces seamlessly with PomodoroPauseEngine / AllowancePauseEngine once
// those land. SessionEngine treats Break phases as "rules are not enforced".
public sealed class NullPauseEngine : IPauseEngine
{
    public PauseState Evaluate(PauseEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new PauseState
        {
            Phase = PausePhase.Focus,
            TimeIntoPhase = input.NowUtc - input.SessionStartUtc,
            PhaseDuration = TimeSpan.MaxValue,
        };
    }
}

// Pure Pomodoro state-machine. Provided for parity; not yet wired into
// SessionEngine.RuleCompiler. Phase 8 follow-up will read PausePolicy from the
// session and route through here.
public sealed class PomodoroPauseEngine : IPauseEngine
{
    public PauseState Evaluate(PauseEvaluationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Type != PauseType.Pomodoro)
            return new NullPauseEngine().Evaluate(input);

        var focus = TimeSpan.FromMinutes(Math.Max(1, input.FocusMinutes ?? 25));
        var brk   = TimeSpan.FromMinutes(Math.Max(1, input.BreakMinutes ?? 5));
        var cycles = Math.Max(1, input.CyclesBeforeLongBreak ?? 4);
        var longBreak = TimeSpan.FromMinutes(Math.Max(1, input.LongBreakMinutes ?? 15));

        var cycleLength = focus + brk;
        var setLength = cycleLength * cycles + (longBreak - brk);

        var elapsed = input.NowUtc - input.SessionStartUtc;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;

        var setOffset = TimeSpan.FromTicks(elapsed.Ticks % setLength.Ticks);

        for (var i = 0; i < cycles; i++)
        {
            var cycleStart = TimeSpan.FromTicks(cycleLength.Ticks * i);
            var cycleEnd = cycleStart + cycleLength;
            if (setOffset < cycleEnd)
            {
                var intoCycle = setOffset - cycleStart;
                if (intoCycle < focus)
                    return new PauseState { Phase = PausePhase.Focus, TimeIntoPhase = intoCycle, PhaseDuration = focus };

                var thisBreak = (i == cycles - 1) ? longBreak : brk;
                return new PauseState { Phase = PausePhase.Break, TimeIntoPhase = intoCycle - focus, PhaseDuration = thisBreak };
            }
        }

        // Should never reach: setOffset < setLength by construction.
        return new PauseState { Phase = PausePhase.Focus, TimeIntoPhase = TimeSpan.Zero, PhaseDuration = focus };
    }
}
