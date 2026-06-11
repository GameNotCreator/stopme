using Microsoft.EntityFrameworkCore;
using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;

namespace StopMe.Infrastructure.Repositories;

public sealed class SessionRepository : ISessionRepository
{
    private readonly StopMeDbContext _db;
    private readonly TimeProvider _clock;

    public SessionRepository(StopMeDbContext db) : this(db, TimeProvider.System) { }

    public SessionRepository(StopMeDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<IReadOnlyList<FocusSession>> GetAllAsync(Guid userProfileId, CancellationToken ct = default) =>
        await _db.FocusSessions
            .AsNoTracking()
            .Include(s => s.SessionBlocklists)
            .Where(s => s.UserProfileId == userProfileId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<FocusSession>> GetEnabledAsync(CancellationToken ct = default) =>
        await _db.FocusSessions
            .AsNoTracking()
            .Include(s => s.SessionBlocklists)
            .Include(s => s.PausePolicy)
            .Where(s => s.Status == SessionStatus.Scheduled
                     || s.Status == SessionStatus.Active
                     || s.IsRecurring)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<FocusSession?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.FocusSessions
            .Include(s => s.SessionBlocklists)
            .Include(s => s.PausePolicy)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            .ConfigureAwait(false);

    public async Task AddAsync(FocusSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        _db.FocusSessions.Add(session);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(FocusSession session, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        _db.FocusSessions.Update(session);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateStatusAsync(Guid sessionId, SessionStatus status, CancellationToken ct = default)
    {
        var session = await _db.FocusSessions.FindAsync(new object?[] { sessionId }, ct).ConfigureAwait(false);
        if (session is null) return;
        session.Status = status;
        session.UpdatedAtUtc = _clock.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.FocusSessions.FindAsync(new object?[] { id }, ct).ConfigureAwait(false);
        if (entity is null) return;
        _db.FocusSessions.Remove(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FocusSession>> GetCompletedBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await _db.FocusSessions
            .AsNoTracking()
            .Where(s => s.Status == SessionStatus.Completed
                     && s.EndAtUtc >= fromUtc
                     && s.EndAtUtc < toUtc)
            .OrderBy(s => s.EndAtUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);
}
