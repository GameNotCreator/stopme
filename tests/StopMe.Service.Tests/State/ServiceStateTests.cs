using FluentAssertions;
using StopMe.Service.State;
using Xunit;

namespace StopMe.Service.Tests.State;

public class ServiceStateTests
{
    [Fact]
    public void Default_state_reports_empty_snapshot()
    {
        var s = new ServiceState();
        s.Current.Should().BeSameAs(RuleSnapshot.Empty);
        s.LastTickUtc.Should().Be(default);
        s.BootRestoreCompleted.Should().BeFalse();
    }

    [Fact]
    public void UpdateSnapshot_persists_both_values()
    {
        var s = new ServiceState();
        var snapshot = new RuleSnapshot(new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc),
            Array.Empty<Core.ValueObjects.ActiveSessionInstance>(),
            Array.Empty<Core.ValueObjects.CompiledRule>());

        s.UpdateSnapshot(snapshot, snapshot.ComputedAtUtc);

        s.Current.Should().BeSameAs(snapshot);
        s.LastTickUtc.Should().Be(snapshot.ComputedAtUtc);
        s.LastTickUtc.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void MarkBootRestoreCompleted_flips_flag()
    {
        var s = new ServiceState();
        s.BootRestoreCompleted.Should().BeFalse();
        s.MarkBootRestoreCompleted();
        s.BootRestoreCompleted.Should().BeTrue();
    }
}
