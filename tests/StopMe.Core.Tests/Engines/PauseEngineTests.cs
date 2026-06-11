using FluentAssertions;
using StopMe.Core.Abstractions;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class PauseEngineTests
{
    [Fact]
    public void NullPauseEngine_always_returns_focus()
    {
        var engine = new NullPauseEngine();
        var state = engine.Evaluate(new PauseEvaluationInput
        {
            SessionId = Guid.NewGuid(),
            SessionStartUtc = DateTime.UtcNow,
            NowUtc = DateTime.UtcNow.AddHours(2),
            Type = PauseType.Pomodoro,
        });
        state.Phase.Should().Be(PausePhase.Focus);
    }

    [Theory]
    [InlineData(0, PausePhase.Focus)]   // start of cycle 1 focus
    [InlineData(24, PausePhase.Focus)]  // 24 min into 25-min focus
    [InlineData(26, PausePhase.Break)]  // 1 min into 5-min break
    [InlineData(30, PausePhase.Focus)]  // start of cycle 2 focus
    [InlineData(115, PausePhase.Break)] // long break: 25*4 focus + 3*5 short breaks = 115. 115th min = into long break
    public void Pomodoro_engine_alternates_focus_and_break(int minutesIntoSession, PausePhase expected)
    {
        var engine = new PomodoroPauseEngine();
        var start = new DateTime(2026, 6, 8, 9, 0, 0, DateTimeKind.Utc);

        var state = engine.Evaluate(new PauseEvaluationInput
        {
            SessionId = Guid.NewGuid(),
            SessionStartUtc = start,
            NowUtc = start.AddMinutes(minutesIntoSession),
            Type = PauseType.Pomodoro,
            FocusMinutes = 25,
            BreakMinutes = 5,
            CyclesBeforeLongBreak = 4,
            LongBreakMinutes = 15,
        });

        state.Phase.Should().Be(expected);
    }

    [Fact]
    public void Pomodoro_engine_falls_back_to_focus_for_non_pomodoro_type()
    {
        var engine = new PomodoroPauseEngine();
        var state = engine.Evaluate(new PauseEvaluationInput
        {
            SessionId = Guid.NewGuid(),
            SessionStartUtc = DateTime.UtcNow,
            NowUtc = DateTime.UtcNow.AddMinutes(30),
            Type = PauseType.None,
        });
        state.Phase.Should().Be(PausePhase.Focus);
    }
}

public class InternetBlockingProviderTests
{
    [Fact]
    public async Task BrowserOnly_provider_apply_is_noop()
    {
        var provider = new BrowserOnlyInternetBlockingProvider();
        provider.Capability.Should().Be("BrowserOnly");
        await provider.ApplyAsync(new InternetBlockingState
        {
            Active = true,
            AllowedDomains = new[] { "docs.google.com" },
        });
        // Reach here without throwing: that's the test.
    }
}
