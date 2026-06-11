using Microsoft.EntityFrameworkCore;
using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;

namespace StopMe.Infrastructure.Repositories;

public sealed class UsageEventRepository : IUsageEventRepository
{
    private readonly StopMeDbContext _db;

    public UsageEventRepository(StopMeDbContext db) => _db = db;

    public async Task AddAsync(UsageEvent usageEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(usageEvent);
        _db.UsageEvents.Add(usageEvent);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UsageEvent>> GetBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await _db.UsageEvents
            .AsNoTracking()
            .Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc)
            .OrderBy(e => e.OccurredAtUtc)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<int> CountByTypeAsync(UsageEventType type, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
        await _db.UsageEvents
            .AsNoTracking()
            .CountAsync(e => e.Type == type && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc, ct)
            .ConfigureAwait(false);

    public async Task<int> DeleteAllAsync(CancellationToken ct = default)
    {
        var deleted = await _db.UsageEvents.ExecuteDeleteAsync(ct).ConfigureAwait(false);
        return deleted;
    }
}
