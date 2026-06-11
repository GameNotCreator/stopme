using Microsoft.EntityFrameworkCore;
using StopMe.Core.Abstractions;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;

namespace StopMe.Infrastructure.Repositories;

public sealed class BlocklistRepository : IBlocklistRepository
{
    private readonly StopMeDbContext _db;

    public BlocklistRepository(StopMeDbContext db) => _db = db;

    public async Task<IReadOnlyList<Blocklist>> GetAllAsync(Guid userProfileId, CancellationToken ct = default) =>
        await _db.Blocklists
            .AsNoTracking()
            .Include(b => b.Rules)
            .Where(b => b.UserProfileId == userProfileId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Blocklist>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var idList = ids.ToArray();
        if (idList.Length == 0) return Array.Empty<Blocklist>();
        return await _db.Blocklists
            .AsNoTracking()
            .Include(b => b.Rules)
            .Where(b => idList.Contains(b.Id))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<Blocklist?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.Blocklists
            .Include(b => b.Rules)
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            .ConfigureAwait(false);

    public async Task AddAsync(Blocklist blocklist, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(blocklist);
        _db.Blocklists.Add(blocklist);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Blocklist blocklist, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(blocklist);
        _db.Blocklists.Update(blocklist);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Blocklists.FindAsync(new object?[] { id }, ct).ConfigureAwait(false);
        if (entity is null) return;
        _db.Blocklists.Remove(entity);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> AppendRulesAsync(Guid blocklistId, IReadOnlyList<BlockRule> rules, DateTime updatedAtUtc, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.Count == 0) return 0;

        var blocklist = await _db.Blocklists.FirstOrDefaultAsync(b => b.Id == blocklistId, ct).ConfigureAwait(false);
        if (blocklist is null) return 0;

        foreach (var rule in rules)
        {
            rule.BlocklistId = blocklistId;
            _db.BlockRules.Add(rule);
        }
        blocklist.UpdatedAtUtc = updatedAtUtc;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return rules.Count;
    }
}
