using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Abstractions;

public interface ISessionEngine
{
    Task<IReadOnlyList<ActiveSessionInstance>> GetActiveSessionsAsync(DateTime nowUtc, CancellationToken ct = default);

    Task StartSessionAsync(Guid sessionId, DateTime nowUtc, CancellationToken ct = default);

    Task StopSessionAsync(Guid sessionId, StopReason reason, DateTime nowUtc, CancellationToken ct = default);

    Task CompleteExpiredSessionsAsync(DateTime nowUtc, CancellationToken ct = default);

    Task<StopAttempt> CanStopSessionAsync(Guid sessionId, StopRequest request, DateTime nowUtc, CancellationToken ct = default);
}
