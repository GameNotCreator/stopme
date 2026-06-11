using StopMe.Core.Enums;
using StopMe.Core.Models;

namespace StopMe.Core.Abstractions;

public interface IBypassEventRepository
{
    Task AddAsync(BypassEvent evt, CancellationToken ct = default);

    Task<IReadOnlyList<BypassEvent>> GetRecentAsync(int limit, CancellationToken ct = default);

    Task<int> CountByTypeAsync(BypassEventType type, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    Task<int> CountBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    Task<IReadOnlyList<BypassEvent>> GetBetweenAsync(DateTime fromUtc, DateTime toUtc, int limit, CancellationToken ct = default);
}
