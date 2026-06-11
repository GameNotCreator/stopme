using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;

namespace StopMe.Core.Tests.Fakes;

internal sealed class FakeSessionRepository : ISessionRepository
{
    public List<FocusSession> Sessions { get; } = new();

    public Task<IReadOnlyList<FocusSession>> GetAllAsync(Guid userProfileId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FocusSession>>(
            Sessions.Where(s => s.UserProfileId == userProfileId).ToList());

    public Task<IReadOnlyList<FocusSession>> GetEnabledAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FocusSession>>(
            Sessions.Where(s => s.Status is SessionStatus.Scheduled or SessionStatus.Active || s.IsRecurring).ToList());

    public Task<FocusSession?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.Id == id));

    public Task AddAsync(FocusSession session, CancellationToken ct = default)
    {
        Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(FocusSession session, CancellationToken ct = default)
    {
        var idx = Sessions.FindIndex(s => s.Id == session.Id);
        if (idx >= 0) Sessions[idx] = session;
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid sessionId, SessionStatus status, CancellationToken ct = default)
    {
        var s = Sessions.FirstOrDefault(x => x.Id == sessionId);
        if (s is not null) s.Status = status;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        Sessions.RemoveAll(s => s.Id == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FocusSession>> GetCompletedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FocusSession>>(
            Sessions.Where(s => s.Status == SessionStatus.Completed
                             && s.EndAtUtc >= fromUtc
                             && s.EndAtUtc < toUtc)
                    .OrderBy(s => s.EndAtUtc)
                    .ToList());
}
