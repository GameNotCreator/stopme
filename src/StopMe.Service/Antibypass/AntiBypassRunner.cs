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

        var hasStrict = active.Any(s => s.Mode == SessionMode.Strict);
        if (!hasStrict)
        {
            _staleEmittedFor.Clear();
            _state.SetClockDriftActive(false);
            return;
        }

        await CheckClockDriftAsync(nowUtc, active, ct).ConfigureAwait(false);
        await CheckExtensionStaleAsync(nowUtc, active, ct).ConfigureAwait(false);
    }

    private async Task CheckClockDriftAsync(DateTime nowUtc, IReadOnlyList<ActiveSessionInstance> active, CancellationToken ct)
    {
        var threshold = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.ClockDriftThresholdSeconds));
        var sample = _drift.Sample(nowUtc, threshold);
        if (sample is null)
        {
            // A clean tick clears the active flag so the lock auto-resolves once
            // the wall clock stabilises again.
            _state.SetClockDriftActive(false);
            return;
        }

        _state.SetClockDriftActive(true);
        _logger.LogWarning("Clock drift detected during strict session: wall={Wall} mono={Mono} drift={Drift}",
            sample.WallDelta, sample.MonotonicDelta, sample.Drift);

        var strict = active.First(s => s.Mode == SessionMode.Strict);
        await RecordAsync(new BypassEvent
        {
            Type = BypassEventType.ClockDrift,
            DetectedAtUtc = nowUtc,
            SessionId = strict.SessionId,
            Source = "ClockDriftDetector",
            Detail = $"Wall delta {sample.WallDelta}, mono delta {sample.MonotonicDelta}, drift {sample.Drift}.",
        }, ct).ConfigureAwait(false);
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
