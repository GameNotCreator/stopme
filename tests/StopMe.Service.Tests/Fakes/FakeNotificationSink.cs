using StopMe.Core.ValueObjects;
using StopMe.Service.Notifications;

namespace StopMe.Service.Tests.Fakes;

internal sealed class FakeNotificationSink : INotificationSink
{
    public List<(AppBlockDecision Decision, RunningAppInfo App)> AppBlocked { get; } = new();
    public List<ActiveSessionInstance> SessionStarted { get; } = new();
    public List<(Guid Id, string Name)> SessionEnded { get; } = new();

    public Task NotifyAppBlockedAsync(AppBlockDecision decision, RunningAppInfo app, CancellationToken ct)
    {
        AppBlocked.Add((decision, app));
        return Task.CompletedTask;
    }

    public Task NotifySessionStartedAsync(ActiveSessionInstance session, CancellationToken ct)
    {
        SessionStarted.Add(session);
        return Task.CompletedTask;
    }

    public Task NotifySessionEndedAsync(Guid sessionId, string name, CancellationToken ct)
    {
        SessionEnded.Add((sessionId, name));
        return Task.CompletedTask;
    }
}
