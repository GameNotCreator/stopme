using Microsoft.Extensions.Logging;
using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.Recurrence;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Engines;

public sealed class SessionEngine : ISessionEngine
{
    private readonly ISessionRepository _sessions;
    private readonly TimeProvider _clock;
    private readonly ILogger<SessionEngine> _logger;
    private readonly IUsageEventRecorder? _recorder;
    private readonly IPauseEngine? _pauseEngine;
    private readonly IClockDriftMonitor _clockDrift;

    public SessionEngine(ISessionRepository sessions, TimeProvider clock, ILogger<SessionEngine> logger)
        : this(sessions, clock, logger, recorder: null, pauseEngine: null, clockDrift: null) { }

    public SessionEngine(
        ISessionRepository sessions,
        TimeProvider clock,
        ILogger<SessionEngine> logger,
        IUsageEventRecorder? recorder)
        : this(sessions, clock, logger, recorder, pauseEngine: null, clockDrift: null) { }

    public SessionEngine(
        ISessionRepository sessions,
        TimeProvider clock,
        ILogger<SessionEngine> logger,
        IUsageEventRecorder? recorder,
        IPauseEngine? pauseEngine)
        : this(sessions, clock, logger, recorder, pauseEngine, clockDrift: null) { }

    public SessionEngine(
        ISessionRepository sessions,
        TimeProvider clock,
        ILogger<SessionEngine> logger,
        IUsageEventRecorder? recorder,
        IPauseEngine? pauseEngine,
        IClockDriftMonitor? clockDrift)
    {
        _sessions = sessions;
        _clock = clock;
        _logger = logger;
        _recorder = recorder;
        _pauseEngine = pauseEngine;
        _clockDrift = clockDrift ?? new NullClockDriftMonitor();
    }

    public async Task<IReadOnlyList<ActiveSessionInstance>> GetActiveSessionsAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var candidates = await _sessions.GetEnabledAsync(ct).ConfigureAwait(false);
        var result = new List<ActiveSessionInstance>();

        foreach (var s in candidates)
        {
            var instance = ResolveActiveInstance(s, nowUtc);
            if (instance is null) continue;
            if (s.AllowPauses && s.PausePolicy is not null && _pauseEngine is not null)
                instance = AttachPausePhase(instance, s, nowUtc);
            result.Add(instance);
        }

        return result;
    }

    private ActiveSessionInstance AttachPausePhase(ActiveSessionInstance instance, FocusSession session, DateTime nowUtc)
    {
        var policy = session.PausePolicy!;
        var state = _pauseEngine!.Evaluate(new PauseEvaluationInput
        {
            SessionId = session.Id,
            SessionStartUtc = instance.StartAtUtc,
            NowUtc = nowUtc,
            Type = policy.Type,
            FocusMinutes = policy.FocusMinutes,
            BreakMinutes = policy.BreakMinutes,
            CyclesBeforeLongBreak = policy.MaxPausesPerSession,
            LongBreakMinutes = policy.PauseDurationMinutes,
            DailyAllowanceMinutes = policy.DailyAllowanceMinutes,
        });

        var remaining = state.Remaining.TotalSeconds;
        var remainingSeconds = remaining > 0 && remaining < int.MaxValue ? (int)remaining : 0;

        return new ActiveSessionInstance
        {
            SessionId = instance.SessionId,
            Name = instance.Name,
            StartAtUtc = instance.StartAtUtc,
            EndAtUtc = instance.EndAtUtc,
            Mode = instance.Mode,
            LockPolicy = instance.LockPolicy,
            BlocklistIds = instance.BlocklistIds,
            PausePhase = state.Phase,
            PausePhaseRemainingSeconds = remainingSeconds,
        };
    }

    public async Task StartSessionAsync(Guid sessionId, DateTime nowUtc, CancellationToken ct = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Session {sessionId} not found.");

        if (session.Status == SessionStatus.Active) return;

        if (!session.IsRecurring && session.EndAtUtc <= nowUtc)
            throw new InvalidOperationException("Cannot start a session whose end time has already passed.");

        await _sessions.UpdateStatusAsync(sessionId, SessionStatus.Active, ct).ConfigureAwait(false);
        _logger.LogInformation("Session {SessionId} ({Name}) started at {NowUtc:o}", session.Id, session.Name, nowUtc);
        await RecordSessionEventAsync(session, UsageEventType.SessionStarted, nowUtc, ct).ConfigureAwait(false);
    }

    public async Task StopSessionAsync(Guid sessionId, StopReason reason, DateTime nowUtc, CancellationToken ct = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, ct).ConfigureAwait(false);
        if (session is null) return;

        var newStatus = reason switch
        {
            StopReason.Expired => SessionStatus.Completed,
            StopReason.UserRequested => SessionStatus.Cancelled,
            StopReason.ReplacedByAnotherSession => SessionStatus.Cancelled,
            StopReason.Error => SessionStatus.Failed,
            StopReason.SystemShutdown => session.Status,
            _ => SessionStatus.Cancelled,
        };

        await _sessions.UpdateStatusAsync(sessionId, newStatus, ct).ConfigureAwait(false);
        _logger.LogInformation("Session {SessionId} stopped at {NowUtc:o} ({Reason})", sessionId, nowUtc, reason);

        var eventType = newStatus switch
        {
            SessionStatus.Completed => UsageEventType.SessionCompleted,
            SessionStatus.Cancelled => UsageEventType.SessionCancelled,
            _ => (UsageEventType?)null,
        };
        if (eventType is not null)
            await RecordSessionEventAsync(session, eventType.Value, nowUtc, ct).ConfigureAwait(false);
    }

    public async Task CompleteExpiredSessionsAsync(DateTime nowUtc, CancellationToken ct = default)
    {
        var enabled = await _sessions.GetEnabledAsync(ct).ConfigureAwait(false);
        var driftActive = _clockDrift.IsClockDriftActive;
        foreach (var s in enabled)
        {
            if (s.IsRecurring) continue;
            if (s.Status is SessionStatus.Completed or SessionStatus.Cancelled) continue;
            if (s.EndAtUtc <= nowUtc)
            {
                // Anti-bypass: while the clock-drift flag is set, refuse to
                // auto-complete a strict session whose EndAtUtc is in the
                // past. Otherwise a single forward jump of the wall-clock
                // would silently end the strict block before AntiBypassRunner
                // ever records a BypassEvent (see the corresponding fix in
                // AntiBypassRunner.RunAsync + ServiceWorker.TickAsync).
                if (driftActive && s.Mode == SessionMode.Strict)
                {
                    _logger.LogWarning(
                        "Skipping auto-completion of strict session {SessionId} ({Name}) — clock drift is active.",
                        s.Id, s.Name);
                    continue;
                }

                await _sessions.UpdateStatusAsync(s.Id, SessionStatus.Completed, ct).ConfigureAwait(false);
                _logger.LogInformation("Session {SessionId} ({Name}) completed at {NowUtc:o}", s.Id, s.Name, nowUtc);
                await RecordSessionEventAsync(s, UsageEventType.SessionCompleted, nowUtc, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task RecordSessionEventAsync(FocusSession session, UsageEventType type, DateTime nowUtc, CancellationToken ct)
    {
        if (_recorder is null) return;
        await _recorder.RecordAsync(new ValueObjects.UsageEventDraft
        {
            Type = type,
            SessionId = session.Id,
            DisplayLabel = session.Name,
        }, nowUtc, ct).ConfigureAwait(false);
    }

    public async Task<StopAttempt> CanStopSessionAsync(Guid sessionId, StopRequest request, DateTime nowUtc, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _sessions.GetByIdAsync(sessionId, ct).ConfigureAwait(false);
        if (session is null) return StopAttempt.Denied("Session not found.");

        // Anti-bypass: when clock drift is active during a strict session, deny
        // every stop attempt regardless of lock policy. AntiBypassRunner sets the
        // flag on the next tick and clears it once the wall clock stabilises.
        if (session.Mode == SessionMode.Strict && _clockDrift.IsClockDriftActive)
            return StopAttempt.Denied("Clock drift detected — strict session cannot be stopped until the system clock stabilises.");

        if (session.Mode == SessionMode.Soft)
            return EvaluateSoftStop(session, request, nowUtc);

        return EvaluateStrictStop(session, request, nowUtc);
    }

    private static StopAttempt EvaluateSoftStop(FocusSession session, StopRequest request, DateTime nowUtc) =>
        session.LockPolicy switch
        {
            LockPolicy.None => StopAttempt.Allowed,
            LockPolicy.Confirmation => request.UserConfirmed
                ? StopAttempt.Allowed
                : StopAttempt.Denied("Confirmation required."),
            LockPolicy.RandomText => RandomTextMatches(request.TypedText, session.RequiredText)
                ? StopAttempt.Allowed
                : StopAttempt.Denied("Stop phrase mismatch."),
            LockPolicy.Password => PasswordHasher.Verify(request.Password, session.PasswordHash)
                ? StopAttempt.Allowed
                : StopAttempt.Denied("Invalid password."),
            LockPolicy.TimeLocked => nowUtc >= session.EndAtUtc
                ? StopAttempt.Allowed
                : StopAttempt.Denied("Session is time-locked."),
            LockPolicy.RestartRequired => StopAttempt.Denied("Restart required to change session."),
            _ => StopAttempt.Denied("Unknown lock policy."),
        };

    private static StopAttempt EvaluateStrictStop(FocusSession session, StopRequest request, DateTime nowUtc)
    {
        // Strict sessions are always implicitly time-locked. Explicit Password/RandomText
        // policies are the only way the user can ever stop before the natural end.
        return session.LockPolicy switch
        {
            LockPolicy.Password => PasswordHasher.Verify(request.Password, session.PasswordHash)
                ? StopAttempt.Allowed
                : StopAttempt.Denied("Invalid password."),
            LockPolicy.RandomText => RandomTextMatches(request.TypedText, session.RequiredText)
                ? StopAttempt.Allowed
                : StopAttempt.Denied("Stop phrase mismatch."),
            _ => nowUtc >= session.EndAtUtc
                ? StopAttempt.Allowed
                : StopAttempt.Denied("Strict mode: cannot stop before session ends."),
        };
    }

    private static bool RandomTextMatches(string? typed, string? required) =>
        !string.IsNullOrEmpty(required)
        && !string.IsNullOrEmpty(typed)
        && string.Equals(typed.Trim(), required.Trim(), StringComparison.Ordinal);

    private static ActiveSessionInstance? ResolveActiveInstance(FocusSession s, DateTime nowUtc)
    {
        var blocklistIds = s.SessionBlocklists?.Select(sb => sb.BlocklistId).ToArray() ?? Array.Empty<Guid>();

        if (!s.IsRecurring)
            return ResolveOneShot(s, nowUtc, blocklistIds);

        return ResolveRecurring(s, nowUtc, blocklistIds);
    }

    private static ActiveSessionInstance? ResolveOneShot(FocusSession s, DateTime nowUtc, Guid[] blocklistIds)
    {
        if (s.Status is SessionStatus.Cancelled or SessionStatus.Completed or SessionStatus.Failed) return null;
        if (nowUtc < s.StartAtUtc || nowUtc >= s.EndAtUtc) return null;
        return new ActiveSessionInstance
        {
            SessionId = s.Id,
            Name = s.Name,
            StartAtUtc = s.StartAtUtc,
            EndAtUtc = s.EndAtUtc,
            Mode = s.Mode,
            LockPolicy = s.LockPolicy,
            BlocklistIds = blocklistIds,
        };
    }

    private static ActiveSessionInstance? ResolveRecurring(FocusSession s, DateTime nowUtc, Guid[] blocklistIds)
    {
        if (string.IsNullOrWhiteSpace(s.RecurrenceRule)) return null;
        // ResolveOneShot honours Status; ResolveRecurring used to silently
        // ignore it, which meant a user-Cancelled (or Failed) recurring
        // session would re-fire on the next tick. StopSessionAsync sets
        // Status to Cancelled — that has to win here too, otherwise stopping
        // a recurring soft session is a no-op.
        if (s.Status is SessionStatus.Cancelled or SessionStatus.Failed) return null;
        if (!RecurrenceParser.TryParse(s.RecurrenceRule, out var pattern) || pattern is null) return null;

        var tz = ResolveTimeZone(s.TimeZoneId);
        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), tz);
        var anchorLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.StartAtUtc, DateTimeKind.Utc), tz);
        var anchorEndLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(s.EndAtUtc, DateTimeKind.Utc), tz);

        var startTime = TimeOnly.FromDateTime(anchorLocal);
        var endTime = TimeOnly.FromDateTime(anchorEndLocal);
        var spansMidnight = endTime <= startTime;

        var anchorDay = DateOnly.FromDateTime(anchorLocal);

        foreach (var candidateDay in EnumerateCandidateDays(nowLocal, spansMidnight))
        {
            if (!pattern.IsActiveOn(candidateDay, anchorDay)) continue;

            var startLocal = candidateDay.ToDateTime(startTime, DateTimeKind.Unspecified);
            var endLocal = (spansMidnight ? candidateDay.AddDays(1) : candidateDay).ToDateTime(endTime, DateTimeKind.Unspecified);

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);

            if (nowUtc >= startUtc && nowUtc < endUtc)
            {
                return new ActiveSessionInstance
                {
                    SessionId = s.Id,
                    Name = s.Name,
                    StartAtUtc = startUtc,
                    EndAtUtc = endUtc,
                    Mode = s.Mode,
                    LockPolicy = s.LockPolicy,
                    BlocklistIds = blocklistIds,
                };
            }
        }

        return null;
    }

    private static IEnumerable<DateOnly> EnumerateCandidateDays(DateTime nowLocal, bool spansMidnight)
    {
        var today = DateOnly.FromDateTime(nowLocal);
        yield return today;
        if (spansMidnight)
            yield return today.AddDays(-1);
    }

    private static TimeZoneInfo ResolveTimeZone(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Utc;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.Utc; }
        catch (InvalidTimeZoneException) { return TimeZoneInfo.Utc; }
    }
}
