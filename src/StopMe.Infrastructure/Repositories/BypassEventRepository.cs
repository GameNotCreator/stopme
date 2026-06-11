using Microsoft.EntityFrameworkCore;
using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;

namespace StopMe.Infrastructure.Repositories;

public sealed class BypassEventRepository : IBypassEventRepository
{
    private readonly StopMeDbContext _db;

    public BypassEventRepository(StopMeDbContext db) => _db = db;

    public async Task AddAsync(BypassEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);
        _db.BypassEvents.Add(evt);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<BypassEvent>> GetRecentAsync(int limit, CancellationToken ct = default)
    {
        if (limit <= 0) return Array.Empty<BypassEvent>();
        return await _db.BypassEvents
            .AsNoTracking()
            .OrderByDescending(e => e.DetectedAtUtc)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<int> CountByTypeAsync(BypassEventType type, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await _db.BypassEvents
            .AsNoTracking()
            .CountAsync(e => e.Type == type && e.DetectedAtUtc >= fromUtc && e.DetectedAtUtc < toUtc, ct)
            .ConfigureAwait(false);

    public async Task<int> CountBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await _db.BypassEvents
            .AsNoTracking()
            .CountAsync(e => e.DetectedAtUtc >= fromUtc && e.DetectedAtUtc < toUtc, ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<BypassEvent>> GetBetweenAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default)
    {
        if (limit <= 0) return Array.Empty<BypassEvent>();
        return await _db.BypassEvents
            .AsNoTracking()
            .Where(e => e.DetectedAtUtc >= fromUtc && e.DetectedAtUtc < toUtc)
            .OrderByDescending(e => e.DetectedAtUtc)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
