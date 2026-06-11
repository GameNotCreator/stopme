using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;
using StopMe.Service.Notifications;

namespace StopMe.Service.Processes;

public sealed class ProcessWatcher
{
    private readonly IProcessProvider _provider;
    private readonly IProcessTerminator _terminator;
    private readonly IAppRuleEngine _engine;
    private readonly INotificationSink _notifications;
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ProcessWatcher> _logger;
    private readonly TimeProvider _clock;

    public ProcessWatcher(
        IProcessProvider provider,
        IProcessTerminator terminator,
        IAppRuleEngine engine,
        INotificationSink notifications,
        IServiceScopeFactory scopes,
        TimeProvider clock,
        ILogger<ProcessWatcher> logger)
    {
        _provider = provider;
        _terminator = terminator;
        _engine = engine;
        _notifications = notifications;
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> EnforceAsync(IReadOnlyList<CompiledRule> rules, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Count == 0) return 0;

        var seen = new HashSet<int>();
        var terminated = 0;
        IUsageEventRecorder? recorder = null;
        AsyncServiceScope? scope = null;

        try
        {
            foreach (var app in _provider.SnapshotRunning())
            {
                ct.ThrowIfCancellationRequested();
                if (!seen.Add(app.ProcessId)) continue;

                var decision = _engine.Evaluate(app, rules);
                if (!decision.IsBlocked) continue;

                var result = await _terminator.CloseOrKillAsync(app.ProcessId, app.ProcessName, ct).ConfigureAwait(false);

                if (result is TerminationResult.AlreadyGone or TerminationResult.AccessDenied or TerminationResult.Failed)
                {
                    if (result is not TerminationResult.AlreadyGone)
                        _logger.LogWarning("Could not terminate process {Pid} ({Name}): {Result}", app.ProcessId, app.ProcessName, result);
                    continue;
                }

                terminated++;
                scope ??= _scopes.CreateAsyncScope();
                recorder ??= scope.Value.ServiceProvider.GetRequiredService<IUsageEventRecorder>();

                await recorder.RecordAsync(new UsageEventDraft
                {
                    Type = UsageEventType.AppBlocked,
                    SessionId = decision.SessionId,
                    RuleId = decision.MatchedRuleId,
                    AppNameLabel = app.ProcessName,
                    DisplayLabel = app.ProcessName,
                }, _clock.GetUtcNow().UtcDateTime, ct).ConfigureAwait(false);

                await _notifications.NotifyAppBlockedAsync(decision, app, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            if (scope.HasValue) await scope.Value.DisposeAsync().ConfigureAwait(false);
        }

        return terminated;
    }
}
