using FluentAssertions;
using StopMe.Service.Antibypass;
using StopMe.Service.Tests.Fakes;
using Xunit;

namespace StopMe.Service.Tests.Antibypass;

public class ExtensionHealthMonitorTests
{
    [Fact]
    public void Records_heartbeat_per_browser()
    {
        var monitor = new ExtensionHealthMonitor(
            new TestOptionsMonitor<AntiBypassOptions>(new AntiBypassOptions { ExtensionStaleAfterSeconds = 60 }));

        var now = new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc);
        monitor.RecordHeartbeat(new ExtensionHeartbeat { Browser = "chrome", ExtensionId = "abc" }, now);
        monitor.RecordHeartbeat(new ExtensionHeartbeat { Browser = "edge", ExtensionId = "def" }, now.AddSeconds(5));

        var all = monitor.SnapshotAll();
        all.Should().HaveCount(2);
        all.Should().Contain(s => s.Browser == "chrome" && s.LastHeartbeatUtc == now);
    }

    [Fact]
    public void Stale_snapshot_returns_browsers_beyond_threshold()
    {
        var monitor = new ExtensionHealthMonitor(
            new TestOptionsMonitor<AntiBypassOptions>(new AntiBypassOptions { ExtensionStaleAfterSeconds = 60 }));

        var now = new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc);
        monitor.RecordHeartbeat(new ExtensionHeartbeat { Browser = "chrome", ExtensionId = "abc" }, now);
        monitor.RecordHeartbeat(new ExtensionHeartbeat { Browser = "edge",   ExtensionId = "def" }, now.AddMinutes(-5));

        var stale = monitor.SnapshotStale(now);

        stale.Should().ContainSingle().Which.Browser.Should().Be("edge");
    }

    [Fact]
    public void Recording_same_browser_updates_timestamp()
    {
        var monitor = new ExtensionHealthMonitor(
            new TestOptionsMonitor<AntiBypassOptions>(new AntiBypassOptions { ExtensionStaleAfterSeconds = 60 }));

        var now = new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc);
        monitor.RecordHeartbeat(new ExtensionHeartbeat { Browser = "chrome", ExtensionId = "abc" }, now);
        monitor.RecordHeartbeat(new ExtensionHeartbeat { Browser = "chrome", ExtensionId = "abc" }, now.AddSeconds(30));

        var snap = monitor.SnapshotAll().Single();
        snap.LastHeartbeatUtc.Should().Be(now.AddSeconds(30));
    }
}
