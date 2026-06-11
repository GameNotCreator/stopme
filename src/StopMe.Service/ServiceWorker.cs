using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;
using StopMe.Service.Antibypass;
using StopMe.Service.Blocking;
using StopMe.Service.Boot;
using StopMe.Service.Notifications;
using StopMe.Service.Processes;
using StopMe.Service.State;

namespace StopMe.Service;

// Implements the §4.2 main loop, plus the Phase 5 AntiBypassRunner sweep.
public sealed class ServiceWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ProcessWatcher _watcher;
    private readonly INotificationSink _notifications;
    private readonly BootRestorer _bootRestorer;
    private readonly AntiBypassRunner _antiBypass;
    private readonly ServiceState _state;
    private readonly TimeProvider _clock;
    private readonly IOptionsMonitor<ServiceOptions> _options;
    private readonly HostsFileInternetBlockingProvider? _hostsBlocker;
    private readonly ILogger<ServiceWorker> _logger;

    private readonly Dictionary<Guid, ActiveSessionInstance> _previouslyActive = new();

    public ServiceWorker(
        IServiceScopeFactory scopes,
        ProcessWatcher watcher,
        INotificationSink notifications,
        BootRestorer bootRestorer,
        AntiBypassRunner antiBypass,
        ServiceState state,
        TimeProvider clock,
        IOptionsMonitor<ServiceOptions> options,
        IInternetBlockingProvider internetBlocking,
        ILogger<ServiceWorker> logger)
    {
        _scopes = scopes;
        _watcher = watcher;
        _notifications = notifications;
        _bootRestorer = bootRestorer;
        _antiBypass = antiBypass;
        _state = state;
        _clock = clock;
        _options = options;
        // We accept any IInternetBlockingProvider for DI flexibility but only
        // the HostsFileInternetBlockingProvider knows how to consume per-tick
        // domain lists — everything else is a contract reservation.
        _hostsBlocker = internetBlocking as HostsFileInternetBlockingProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ServiceWorker starting…");

        try
        {
            await _bootRestorer.RestoreAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Boot restoration failed; continuing with stale state.");
            _state.MarkBootRestoreCompleted();
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tick failed; will retry next interval.");
            }

            var interval = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.TickIntervalSeconds));
            try
            {
                await Task.Delay(interval, _clock, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    internal async Task TickAsync(CancellationToken ct)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;

        IReadOnlyList<ActiveSessionInstance> active;
        IReadOnlyList<CompiledRule> rules;

        await using (var scope = _scopes.CreateAsyncScope())
        {
            var engine = scope.ServiceProvider.GetRequiredService<ISessionEngine>();
            var compiler = scope.ServiceProvider.GetRequiredService<IRuleCompiler>();

            // Take a pre-completion view of active sessions. Anti-bypass runs
            // FIRST so a clock-jump that would expire a strict session in
            // CompleteExpiredSessionsAsync sets the drift flag in time for
            // the engine to skip the strict completion below. A previous
            // version ran anti-bypass at the END of the tick, by which point
            // the strict session was already silently completed.
            var preCompletionActive = await engine.GetActiveSessionsAsync(nowUtc, ct).ConfigureAwait(false);
            await _antiBypass.RunAsync(nowUtc, preCompletionActive, ct).ConfigureAwait(false);

            await engine.CompleteExpiredSessionsAsync(nowUtc, ct).ConfigureAwait(false);
            active = await engine.GetActiveSessionsAsync(nowUtc, ct).ConfigureAwait(false);
            rules = await compiler.CompileForSessionsAsync(active, ct).ConfigureAwait(false);
        }

        _state.UpdateSnapshot(new RuleSnapshot(nowUtc, active, rules), nowUtc);

        await PublishLifecycleNotificationsAsync(active, ct).ConfigureAwait(false);

        var terminated = await _watcher.EnforceAsync(rules, ct).ConfigureAwait(false);
        if (terminated > 0)
            _logger.LogInformation("Tick terminated {Count} blocked process(es).", terminated);

        // Push the current blocked-domains set into the hosts file (or remove
        // it if the session ended). Cheap diff inside — only writes when set
        // changes between ticks.
        if (_hostsBlocker is not null)
        {
            var blockedDomains = ExtractDomains(rules);
            await _hostsBlocker.ApplyDomainsAsync(blockedDomains, ct).ConfigureAwait(false);
        }
    }

    private static HashSet<string> ExtractDomains(IReadOnlyList<CompiledRule> rules)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var exceptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var r in rules)
        {
            switch (r.Type)
            {
                case RuleType.Domain:
                    set.Add(r.Value);
                    break;
                case RuleType.ExceptionDomain:
                    exceptions.Add(r.Value);
                    break;
                // Url / Wildcard / Category rules deliberately do NOT land in
                // the hosts file. The previous Url handler pulled out the host
                // and added it, which silently blocked the entire domain when
                // the user only meant to block a specific path. Path- and
                // pattern-scoped rules are enforced by the browser extension.
            }
        }
        foreach (var e in exceptions) set.Remove(e);
        return set;
    }

    private async Task PublishLifecycleNotificationsAsync(IReadOnlyList<ActiveSessionInstance> active, CancellationToken ct)
    {
        var currentIds = active.ToDictionary(s => s.SessionId);

        foreach (var id in _previouslyActive.Keys.Except(currentIds.Keys).ToArray())
        {
            await _notifications.NotifySessionEndedAsync(id, _previouslyActive[id].Name, ct).ConfigureAwait(false);
            _previouslyActive.Remove(id);
        }

        foreach (var (id, session) in currentIds.Where(kv => !_previouslyActive.ContainsKey(kv.Key)))
        {
            await _notifications.NotifySessionStartedAsync(session, ct).ConfigureAwait(false);
            _previouslyActive[id] = session;
        }

        foreach (var (id, session) in currentIds)
            _previouslyActive[id] = session;
    }
}
