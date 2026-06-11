using StopMe.Core.Enums;

namespace StopMe.Core.Abstractions;

// Phase 8 placeholder for §24 sync preparation. The contract reserves the
// shape of the work item so when a backend lands the wire format doesn't
// shift. No implementation is registered today.
public interface ISyncOutbox
{
    Task EnqueueAsync(SyncWorkItem item, CancellationToken ct = default);

    Task<IReadOnlyList<SyncWorkItem>> DrainAsync(int max, CancellationToken ct = default);
}

public sealed class SyncWorkItem
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string EntityType { get; init; }  // "blocklist", "session", "settings"
    public required Guid EntityId { get; init; }
    public required SyncOperation Operation { get; init; }
    public required DateTime LastModifiedUtc { get; init; }
    public Guid? RemoteId { get; init; }
    public string? PayloadJson { get; init; }
}

public enum SyncOperation
{
    Upsert = 0,
    Delete = 1,
}

// Sync metadata held next to a synced entity. Stored separately so existing
// entities don't need to grow new columns until the V2 backend ships.
public sealed class SyncMetadata
{
    public required string EntityType { get; init; }
    public required Guid LocalId { get; init; }
    public Guid? RemoteId { get; init; }
    public SyncStatus Status { get; init; } = SyncStatus.Local;
    public DateTime LastSyncedUtc { get; init; }
}

public enum SyncStatus
{
    Local = 0,
    PendingUpload = 1,
    Synced = 2,
    Conflict = 3,
}
