using StopMe.Core.Models;

namespace StopMe.Core.Abstractions;

public interface IUsageEventRepository
{
    Task AddAsync(UsageEvent usageEvent, CancellationToken ct = default);

    Task<IReadOnlyList<UsageEvent>> GetBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    Task<int> CountByTypeAsync(Enums.UsageEventType type, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

    // For full data resets initiated from the privacy panel.
    Task<int> DeleteAllAsync(CancellationToken ct = default);
}
