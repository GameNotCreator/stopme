using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Service.State;

namespace StopMe.Service.Boot;

// On startup: settle session status for what survived a reboot. Strict-mode sessions that
// are still inside their time window are re-marked Active so the worker resumes blocking
// immediately. One-shot sessions whose end has passed are marked Completed.
public sealed class BootRestorer
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ServiceState _state;
    private readonly TimeProvider _clock;
    private readonly ILogger<BootRestorer> _logger;

    public BootRestorer(IServiceScopeFactory scopes, ServiceState state, TimeProvider clock, ILogger<BootRestorer> logger)
    {
        _scopes = scopes;
        _state = state;
        _clock = clock;
        _logger = logger;
    }

    public async Task RestoreAsync(CancellationToken ct)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        await using var scope = _scopes.CreateAsyncScope();
        var sessions = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        var engine = scope.ServiceProvider.GetRequiredService<ISessionEngine>();

        var enabled = await sessions.GetEnabledAsync(ct).ConfigureAwait(false);
        var restored = 0;
        var completed = 0;

        foreach (var s in enabled)
        {
            ct.ThrowIfCancellationRequested();
            if (s.IsRecurring) continue;

            if (s.EndAtUtc <= nowUtc)
            {
                if (s.Status is not SessionStatus.Completed and not SessionStatus.Cancelled)
                {
                    await sessions.UpdateStatusAsync(s.Id, SessionStatus.Completed, ct).ConfigureAwait(false);
                    completed++;
                }
                continue;
            }

            // The session is still inside its window: should it be Active?
            if (nowUtc >= s.StartAtUtc && s.Status != SessionStatus.Active)
            {
                await sessions.UpdateStatusAsync(s.Id, SessionStatus.Active, ct).ConfigureAwait(false);
                restored++;
                _logger.LogInformation("Restored {Mode} session {SessionId} ({Name}) after reboot.",
                    s.Mode, s.Id, s.Name);
            }
        }

        // Also let the engine purge anything else that needs completing.
        await engine.CompleteExpiredSessionsAsync(nowUtc, ct).ConfigureAwait(false);

        _state.MarkBootRestoreCompleted();
        _logger.LogInformation(
            "Boot restoration complete: {Restored} session(s) resumed, {Completed} expired.",
            restored, completed);
    }
}
