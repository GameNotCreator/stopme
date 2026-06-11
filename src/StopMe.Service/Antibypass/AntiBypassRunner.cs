using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.ValueObjects;
using StopMe.Service.State;

namespace StopMe.Service.Antibypass;

public sealed class AntiBypassRunner
{
    private readonly IExtensionHealthMonitor _health;
    private readonly ClockDriftDetector _drift;
    private readonly IServiceScopeFactory _scopes;
    private readonly ServiceState _state;
    private readonly IOptionsMonitor<AntiBypassOptions> _options;
    private readonly ILogger<AntiBypassRunner> _logger;

    private readonly HashSet<string> _staleEmittedFor = new(StringComparer.OrdinalIgnoreCase);

    // Tracks the wall-clock time of the most recent positive drift sample.
    // Used to enforce ClockDriftCooldownSeconds — see RunAsync.
    private DateTime? _lastDriftDetectedUtc;

    public AntiBypassRunner(
        IExtensionHealthMonitor health,
        ClockDriftDetector drift,
        IServiceScopeFactory scopes,
        ServiceState state,
        IOptionsMonitor<AntiBypassOptions> options,
        ILogger<AntiBypassRunner> logger)
    {
        _health = health;
        _drift = drift;
        _scopes = scopes;
        _state = state;
        _options = options;
        _logger = logger;
    }

    public async Task RunAsync(DateTime nowUtc, IReadOnlyList<ActiveSessionInstance> active, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(active);

        // Sample drift EVERY tick, regardless of whether a strict session is
        // currently active. A wall-clock jump that would expire a strict
        // session is detected here BEFORE the worker calls
        // CompleteExpiredSessionsAsync, so the drift flag is set in time to
        // protect the strict session.
        await CheckClockDriftAsync(nowUtc, active, ct).ConfigureAwait(false);

        var hasStrict = active.Any(s => s.Mode == SessionMode.Strict);
        if (!hasStrict)
        {
            _staleEmittedFor.Clear();
            // Do NOT clear the drift flag here. The pre-existing implementation
            // cleared it whenever no strict session was active, which is exactly
            // the state immediately after a successful clock-jump bypass — the
            // flag would be cleared faster than it could ever be observed.
            return;
        }

        await CheckExtensionStaleAsync(nowUtc, active, ct).ConfigureAwait(false);
    }

    private async Task CheckClockDriftAsync(DateTime nowUtc, IReadOnlyList<ActiveSessionInstance> active, CancellationToken ct)
    {
        var threshold = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.ClockDriftThresholdSeconds));
        var cooldown = TimeSpan.FromSeconds(Math.Max(0, _options.CurrentValue.ClockDriftCooldownSeconds));
        var sample = _drift.Sample(nowUtc, threshold);

        if (sample is null)
        {
            // Hysteresis: keep the flag set for `cooldown` after the last
            // observed drift. Without this, a single jump-then-stabilise
            // sequence (jump, then leave the clock alone) would clear the
            // flag on the very next tick before CompleteExpiredSessionsAsync
            // can act on it.
            if (_lastDriftDetectedUtc is null) return;
            if (nowUtc - _lastDriftDetectedUtc.Value <= cooldown) return;

            _state.SetClockDriftActive(false);
            _lastDriftDetectedUtc = null;
            return;
        }

        _state.SetClockDriftActive(true);
        _lastDriftDetectedUtc = nowUtc;

        _logger.LogWarning("Clock drift detected: wall={Wall} mono={Mono} drift={Drift}",
            sample.WallDelta, sample.MonotonicDelta, sample.Drift);

        // BypassEvent only carries useful detail when a strict session is the
        // would-be victim. Drift outside a strict window is logged but not
        // persisted.
        var strictSessionId = active.FirstOrDefault(s => s.Mode == SessionMode.Strict)?.SessionId
            ?? await FindRecentStrictCandidateAsync(ct).ConfigureAwait(false);
        if (strictSessionId is null) return;

        await RecordAsync(new BypassEvent
        {
            Type = BypassEventType.ClockDrift,
            DetectedAtUtc = nowUtc,
            SessionId = strictSessionId.Value,
            Source = "ClockDriftDetector",
            Detail = $"Wall delta {sample.WallDelta}, mono delta {sample.MonotonicDelta}, drift {sample.Drift}.",
        }, ct).ConfigureAwait(false);
    }

    // After a forward clock-jump the strict session may already be outside its
    // [StartAt, EndAt) window when the engine resolves "active", which leaves
    // the active list empty even though the bypass is targeting a real strict
    // session. Walk the session repo to attribute the BypassEvent to the most
    // likely victim — any enabled, non-finalised strict session.
    private async Task<Guid?> FindRecentStrictCandidateAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
            var enabled = await repo.GetEnabledAsync(ct).ConfigureAwait(false);
            return enabled
                .Where(s => s.Mode == SessionMode.Strict)
                .Where(s => s.Status is not (SessionStatus.Completed or SessionStatus.Cancelled or SessionStatus.Failed))
                .Select(s => (Guid?)s.Id)
                .FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "FindRecentStrictCandidateAsync failed; skipping BypassEvent.");
            return null;
        }
    }

    private async Task CheckExtensionStaleAsync(DateTime nowUtc, IReadOnlyList<ActiveSessionInstance> active, CancellationToken ct)
    {
        var stale = _health.SnapshotStale(nowUtc);
        if (stale.Count == 0)
        {
            _staleEmittedFor.Clear();
            return;
        }

        var strict = active.First(s => s.Mode == SessionMode.Strict);
        foreach (var browser in stale)
        {
            if (!_staleEmittedFor.Add(browser.Browser)) continue;
            await RecordAsync(new BypassEvent
            {
                Type = BypassEventType.ExtensionStale,
                DetectedAtUtc = nowUtc,
                SessionId = strict.SessionId,
                Source = browser.Browser,
                Detail = $"No heartbeat from extension {browser.ExtensionId} since {browser.LastHeartbeatUtc:O}.",
            }, ct).ConfigureAwait(false);
        }
    }

    private async Task RecordAsync(BypassEvent evt, CancellationToken ct)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IBypassEventRepository>();
            await repo.AddAsync(evt, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist bypass event {Type}", evt.Type);
        }
    }
}
