using StopMe.Core.ValueObjects;

namespace StopMe.Service.Notifications;

public interface INotificationSink
{
    Task NotifyAppBlockedAsync(AppBlockDecision decision, RunningAppInfo app, CancellationToken ct);

    Task NotifySessionStartedAsync(ActiveSessionInstance session, CancellationToken ct);

    Task NotifySessionEndedAsync(Guid sessionId, string name, CancellationToken ct);
}
