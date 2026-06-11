using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StopMe.Core.Abstractions;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.Tests.Fakes;
using StopMe.Core.ValueObjects;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class ClockDriftBypassPreventionTests
{
    [Fact]
    public async Task Strict_session_cannot_be_stopped_while_clock_drift_is_active()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero));
        var repo = new FakeSessionRepository();
        var sessionId = Guid.NewGuid();
        repo.Sessions.Add(new FocusSession
        {
            Id = sessionId,
            Name = "Lock",
            Status = SessionStatus.Active,
            Mode = SessionMode.Strict,
            LockPolicy = LockPolicy.RandomText,
            RequiredText = "stop",
            StartAtUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(-10),
            EndAtUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(50),
            TimeZoneId = "UTC",
        });

        var drift = new ToggleDriftMonitor { Active = true };
        var engine = new SessionEngine(repo, clock, NullLogger<SessionEngine>.Instance,
            recorder: null, pauseEngine: null, clockDrift: drift);

        var attempt = await engine.CanStopSessionAsync(sessionId,
            new StopRequest { Reason = StopReason.UserRequested, TypedText = "stop" }, clock.GetUtcNow().UtcDateTime);
        attempt.IsAllowed.Should().BeFalse();
        attempt.DenialReason.Should().Contain("Clock drift");
    }

    [Fact]
    public async Task Strict_session_stop_resumes_when_clock_drift_clears()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero));
        var repo = new FakeSessionRepository();
        var sessionId = Guid.NewGuid();
        repo.Sessions.Add(new FocusSession
        {
            Id = sessionId,
            Name = "Lock",
            Status = SessionStatus.Active,
            Mode = SessionMode.Strict,
            LockPolicy = LockPolicy.RandomText,
            RequiredText = "stop",
            StartAtUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(-10),
            EndAtUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(50),
            TimeZoneId = "UTC",
        });

        var drift = new ToggleDriftMonitor { Active = false };
        var engine = new SessionEngine(repo, clock, NullLogger<SessionEngine>.Instance,
            recorder: null, pauseEngine: null, clockDrift: drift);

        var attempt = await engine.CanStopSessionAsync(sessionId,
            new StopRequest { Reason = StopReason.UserRequested, TypedText = "stop" }, clock.GetUtcNow().UtcDateTime);
        attempt.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Soft_session_unaffected_by_clock_drift_flag()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero));
        var repo = new FakeSessionRepository();
        var sessionId = Guid.NewGuid();
        repo.Sessions.Add(new FocusSession
        {
            Id = sessionId,
            Name = "Soft",
            Status = SessionStatus.Active,
            Mode = SessionMode.Soft,
            LockPolicy = LockPolicy.Confirmation,
            StartAtUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(-10),
            EndAtUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(50),
            TimeZoneId = "UTC",
        });

        var drift = new ToggleDriftMonitor { Active = true };
        var engine = new SessionEngine(repo, clock, NullLogger<SessionEngine>.Instance,
            recorder: null, pauseEngine: null, clockDrift: drift);

        var attempt = await engine.CanStopSessionAsync(sessionId,
            new StopRequest { Reason = StopReason.UserRequested, UserConfirmed = true }, clock.GetUtcNow().UtcDateTime);
        attempt.IsAllowed.Should().BeTrue();
    }

    private sealed class ToggleDriftMonitor : IClockDriftMonitor
    {
        public bool Active { get; set; }
        public bool IsClockDriftActive => Active;
    }
}
