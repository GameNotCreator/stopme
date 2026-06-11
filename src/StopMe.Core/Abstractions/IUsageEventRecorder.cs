using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Abstractions;

// Privacy-aware façade over IUsageEventRepository. Reads the current
// StatsRetentionPolicy from settings, then filters/hashes the draft accordingly:
//   - Readable   : DisplayLabel + hashes persisted
//   - Anonymized : hashes only (DisplayLabel dropped)
//   - Disabled   : no write
public interface IUsageEventRecorder
{
    Task<bool> RecordAsync(UsageEventDraft draft, DateTime occurredAtUtc, CancellationToken ct = default);

    Task<StatsRetentionPolicy> GetActivePolicyAsync(CancellationToken ct = default);

    Task SetActivePolicyAsync(StatsRetentionPolicy policy, CancellationToken ct = default);
}
