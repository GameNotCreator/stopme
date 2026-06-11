using Microsoft.Extensions.Logging;
using StopMe.Core.ValueObjects;

namespace StopMe.Service.Notifications;

// Phase 2 sink: emits structured log records. Phase 6 will add a UserToastSink that
// forwards to the WinUI tray app via the local API.
public sealed class LogNotificationSink : INotificationSink
{
    private readonly ILogger<LogNotificationSink> _logger;

    public LogNotificationSink(ILogger<LogNotificationSink> logger) => _logger = logger;

    public Task NotifyAppBlockedAsync(AppBlockDecision decision, RunningAppInfo app, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(app);
        _logger.LogInformation(
            "Blocked app {ProcessName} (pid {Pid}) by rule {RuleType} during session {SessionName}",
            app.ProcessName, app.ProcessId, decision.MatchedRuleType, decision.SessionName ?? "(none)");
        return Task.CompletedTask;
    }

    public Task NotifySessionStartedAsync(ActiveSessionInstance session, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        _logger.LogInformation(
            "Session started: {Name} ({SessionId}) until {EndAtUtc:o} ({Mode}, {LockPolicy})",
            session.Name, session.SessionId, session.EndAtUtc, session.Mode, session.LockPolicy);
        return Task.CompletedTask;
    }

    public Task NotifySessionEndedAsync(Guid sessionId, string name, CancellationToken ct)
    {
        _logger.LogInformation("Session ended: {Name} ({SessionId})", name, sessionId);
        return Task.CompletedTask;
    }
}
