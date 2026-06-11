namespace StopMe.Service.Antibypass;

public sealed class ExtensionHeartbeat
{
    public required string Browser { get; init; }
    public required string ExtensionId { get; init; }
    public string? ExtensionVersion { get; init; }
}

public sealed record ExtensionBrowserSnapshot(
    string Browser,
    string ExtensionId,
    string? ExtensionVersion,
    DateTime LastHeartbeatUtc);

public interface IExtensionHealthMonitor
{
    void RecordHeartbeat(ExtensionHeartbeat heartbeat, DateTime nowUtc);

    IReadOnlyList<ExtensionBrowserSnapshot> SnapshotAll();

    IReadOnlyList<ExtensionBrowserSnapshot> SnapshotStale(DateTime nowUtc);
}
