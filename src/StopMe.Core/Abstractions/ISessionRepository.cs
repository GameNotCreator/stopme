using StopMe.Core.Models;

namespace StopMe.Core.Abstractions;

public interface ISessionRepository
{
    Task<IReadOnlyList<FocusSession>> GetAllAsync(Guid userProfileId, CancellationToken ct = default);

    Task<IReadOnlyList<FocusSession>> GetEnabledAsync(CancellationToken ct = default);

    Task<FocusSession?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(FocusSession session, CancellationToken ct = default);

    Task UpdateAsync(FocusSession session, CancellationToken ct = default);

    Task UpdateStatusAsync(Guid sessionId, Enums.SessionStatus status, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<FocusSession>> GetCompletedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);
}
