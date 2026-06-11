using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace StopMe.Service.Antibypass;

public sealed class ExtensionHealthMonitor : IExtensionHealthMonitor
{
    private readonly ConcurrentDictionary<string, ExtensionBrowserSnapshot> _byBrowser = new(StringComparer.OrdinalIgnoreCase);
    private readonly IOptionsMonitor<AntiBypassOptions> _options;

    public ExtensionHealthMonitor(IOptionsMonitor<AntiBypassOptions> options) => _options = options;

    public void RecordHeartbeat(ExtensionHeartbeat heartbeat, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(heartbeat);
        _byBrowser[heartbeat.Browser] = new ExtensionBrowserSnapshot(
            heartbeat.Browser,
            heartbeat.ExtensionId,
            heartbeat.ExtensionVersion,
            nowUtc);
    }

    public IReadOnlyList<ExtensionBrowserSnapshot> SnapshotAll() => _byBrowser.Values.ToArray();

    public IReadOnlyList<ExtensionBrowserSnapshot> SnapshotStale(DateTime nowUtc)
    {
        var threshold = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.ExtensionStaleAfterSeconds));
        return _byBrowser.Values
            .Where(s => nowUtc - s.LastHeartbeatUtc > threshold)
            .ToArray();
    }
}
