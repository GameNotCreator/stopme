using StopMe.Core.Models;

namespace StopMe.Core.Abstractions;

public interface IBlocklistRepository
{
    Task<IReadOnlyList<Blocklist>> GetAllAsync(Guid userProfileId, CancellationToken ct = default);

    Task<IReadOnlyList<Blocklist>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default);

    Task<Blocklist?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Blocklist blocklist, CancellationToken ct = default);

    Task UpdateAsync(Blocklist blocklist, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    // Appends rules to a tracked Blocklist + persists. Cleaner than calling
    // Update on a tracked entity with new navigation children.
    Task<int> AppendRulesAsync(Guid blocklistId, IReadOnlyList<BlockRule> rules, DateTime updatedAtUtc, CancellationToken ct = default);
}
