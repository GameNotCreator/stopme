using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.Tests.Fakes;
using StopMe.Core.ValueObjects;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class SessionEngineTests
{
    private readonly FakeSessionRepository _repo = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero));
    private readonly SessionEngine _engine;

    public SessionEngineTests()
    {
        _engine = new SessionEngine(_repo, _clock, NullLogger<SessionEngine>.Instance);
    }

    [Fact]
    public async Task GetActive_returns_oneshot_session_in_window()
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var session = OneShot(start: nowUtc.AddMinutes(-10), end: nowUtc.AddMinutes(50));
        _repo.Sessions.Add(session);

        var active = await _engine.GetActiveSessionsAsync(nowUtc);

        active.Should().ContainSingle().Which.SessionId.Should().Be(session.Id);
    }

    [Fact]
    public async Task GetActive_skips_oneshot_session_outside_window()
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var pastSession = OneShot(start: nowUtc.AddHours(-3), end: nowUtc.AddHours(-1));
        var futureSession = OneShot(start: nowUtc.AddHours(1), end: nowUtc.AddHours(3));
        _repo.Sessions.AddRange(new[] { pastSession, futureSession });

        var active = await _engine.GetActiveSessionsAsync(nowUtc);

        active.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActive_skips_cancelled_oneshot_session()
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var session = OneShot(start: nowUtc.AddMinutes(-10), end: nowUtc.AddMinutes(50));
        session.Status = SessionStatus.Cancelled;
        _repo.Sessions.Add(session);

        (await _engine.GetActiveSessionsAsync(nowUtc)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetActive_includes_recurring_session_on_matching_day_and_time()
    {
        // Anchor 2026-06-01 (Monday) 08:00 UTC to 12:00 UTC. Recurrence MO-FR. Now 2026-06-08 09:00 UTC = Monday.
        var anchorStart = new DateTime(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);
        var anchorEnd = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        var session = Recurring(anchorStart, anchorEnd, "FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR");
        _repo.Sessions.Add(session);

        var active = await _engine.GetActiveSessionsAsync(_clock.GetUtcNow().UtcDateTime);

        active.Should().ContainSingle();
    }

    [Fact]
    public async Task GetActive_excludes_recurring_session_on_non_matching_day()
    {
        // Now 2026-06-08 = Monday; restrict to weekends only.
        var anchorStart = new DateTime(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc);   // Saturday
        var anchorEnd = new DateTime(2026, 6, 6, 11, 0, 0, DateTimeKind.Utc);
        var session = Recurring(anchorStart, anchorEnd, "FREQ=WEEKLY;BYDAY=SA,SU");
        _repo.Sessions.Add(session);

        (await _engine.GetActiveSessionsAsync(_clock.GetUtcNow().UtcDateTime)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetActive_excludes_recurring_session_outside_time_of_day()
    {
        // Window 14:00-16:00 UTC daily. Now 09:00 UTC.
        var anchorStart = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc);
        var anchorEnd = new DateTime(2026, 6, 1, 16, 0, 0, DateTimeKind.Utc);
        var session = Recurring(anchorStart, anchorEnd, "FREQ=DAILY");
        _repo.Sessions.Add(session);

        (await _engine.GetActiveSessionsAsync(_clock.GetUtcNow().UtcDateTime)).Should().BeEmpty();
    }

    [Fact]
    public async Task Strict_mode_denies_stop_before_end()
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var session = OneShot(start: nowUtc.AddMinutes(-10), end: nowUtc.AddMinutes(50));
        session.Mode = SessionMode.Strict;
        _repo.Sessions.Add(session);

        var attempt = await _engine.CanStopSessionAsync(session.Id,
            new StopRequest { Reason = StopReason.UserRequested, UserConfirmed = true }, nowUtc);

        attempt.IsAllowed.Should().BeFalse();
        attempt.DenialReason.Should().Contain("Strict");
    }

    [Fact]
    public async Task Strict_mode_with_password_allows_stop_with_correct_password()
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var session = OneShot(start: nowUtc.AddMinutes(-10), end: nowUtc.AddMinutes(50));
        session.Mode = SessionMode.Strict;
        session.LockPolicy = LockPolicy.Password;
        session.PasswordHash = PasswordHasher.Hash("focus123");
        _repo.Sessions.Add(session);

        var ok = await _engine.CanStopSessionAsync(session.Id,
            new StopRequest { Reason = StopReason.UserRequested, Password = "focus123" }, nowUtc);
        ok.IsAllowed.Should().BeTrue();

        var nope = await _engine.CanStopSessionAsync(session.Id,
            new StopRequest { Reason = StopReason.UserRequested, Password = "wrong" }, nowUtc);
        nope.IsAllowed.Should().BeFalse();
    }

    [Fact]
    public async Task Soft_mode_with_confirmation_requires_user_to_confirm()
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var session = OneShot(start: nowUtc.AddMinutes(-10), end: nowUtc.AddMinutes(50));
        session.Mode = SessionMode.Soft;
        session.LockPolicy = LockPolicy.Confirmation;
        _repo.Sessions.Add(session);

        var denied = await _engine.CanStopSessionAsync(session.Id,
            new StopRequest { Reason = StopReason.UserRequested }, nowUtc);
        denied.IsAllowed.Should().BeFalse();

        var ok = await _engine.CanStopSessionAsync(session.Id,
            new StopRequest { Reason = StopReason.UserRequested, UserConfirmed = true }, nowUtc);
        ok.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Random_text_lock_requires_exact_typed_phrase()
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var session = OneShot(start: nowUtc.AddMinutes(-10), end: nowUtc.AddMinutes(50));
        session.Mode = SessionMode.Strict;
        session.LockPolicy = LockPolicy.RandomText;
        session.RequiredText = "I choose to stop focusing.";
        _repo.Sessions.Add(session);

        var typo = await _engine.CanStopSessionAsync(session.Id,
            new StopRequest { Reason = StopReason.UserRequested, TypedText = "i choose to stop focusing" }, nowUtc);
        typo.IsAllowed.Should().BeFalse();

        var ok = await _engine.CanStopSessionAsync(session.Id,
            new StopRequest { Reason = StopReason.UserRequested, TypedText = "I choose to stop focusing." }, nowUtc);
        ok.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task Complete_expired_sessions_updates_status()
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var session = OneShot(start: nowUtc.AddHours(-3), end: nowUtc.AddHours(-1));
        session.Status = SessionStatus.Active;
        _repo.Sessions.Add(session);

        await _engine.CompleteExpiredSessionsAsync(nowUtc);

        _repo.Sessions.Single().Status.Should().Be(SessionStatus.Completed);
    }

    [Fact]
    public async Task Start_session_throws_if_already_expired()
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var session = OneShot(start: nowUtc.AddHours(-3), end: nowUtc.AddHours(-1));
        _repo.Sessions.Add(session);

        Func<Task> act = () => _engine.StartSessionAsync(session.Id, nowUtc);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static FocusSession OneShot(DateTime start, DateTime end) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserProfileId = Guid.NewGuid(),
            Name = "Test Session",
            StartAtUtc = start,
            EndAtUtc = end,
            Status = SessionStatus.Scheduled,
            Mode = SessionMode.Soft,
            LockPolicy = LockPolicy.None,
            IsRecurring = false,
            TimeZoneId = "UTC",
        };

    private static FocusSession Recurring(DateTime anchorStartUtc, DateTime anchorEndUtc, string rrule) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserProfileId = Guid.NewGuid(),
            Name = "Recurring Session",
            StartAtUtc = anchorStartUtc,
            EndAtUtc = anchorEndUtc,
            Status = SessionStatus.Scheduled,
            Mode = SessionMode.Soft,
            LockPolicy = LockPolicy.None,
            IsRecurring = true,
            RecurrenceRule = rrule,
            TimeZoneId = "UTC",
        };
}
