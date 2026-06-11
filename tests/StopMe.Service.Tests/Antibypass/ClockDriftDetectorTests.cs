using FluentAssertions;
using StopMe.Service.Antibypass;
using Xunit;

namespace StopMe.Service.Tests.Antibypass;

public class ClockDriftDetectorTests
{
    [Fact]
    public void First_sample_returns_no_drift()
    {
        long mono = 0;
        var detector = new ClockDriftDetector(() => mono);
        var nowUtc = new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc);

        detector.Sample(nowUtc, TimeSpan.FromSeconds(60)).Should().BeNull();
    }

    [Fact]
    public void Wall_clock_jump_forward_is_detected()
    {
        long mono = 0;
        var detector = new ClockDriftDetector(() => mono);
        var first = new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc);

        detector.Sample(first, TimeSpan.FromSeconds(60));

        // Two seconds of monotonic time pass, but the wall clock jumped 20 minutes.
        mono += 2000;
        var jumped = first.AddMinutes(20).AddSeconds(2);

        var drift = detector.Sample(jumped, TimeSpan.FromSeconds(60));

        drift.Should().NotBeNull();
        drift!.WallDelta.Should().BeCloseTo(TimeSpan.FromMinutes(20).Add(TimeSpan.FromSeconds(2)), TimeSpan.FromSeconds(1));
        drift.MonotonicDelta.Should().BeCloseTo(TimeSpan.FromSeconds(2), TimeSpan.FromMilliseconds(50));
        drift.Drift.Should().BeCloseTo(TimeSpan.FromMinutes(20), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Wall_clock_jump_backward_is_detected()
    {
        long mono = 0;
        var detector = new ClockDriftDetector(() => mono);
        var first = new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc);

        detector.Sample(first, TimeSpan.FromSeconds(60));

        mono += 5000;
        var jumped = first.AddMinutes(-30);

        var drift = detector.Sample(jumped, TimeSpan.FromSeconds(60));
        drift.Should().NotBeNull();
        drift!.Drift.Duration().Should().BeGreaterThan(TimeSpan.FromMinutes(29));
    }

    [Fact]
    public void Small_natural_drift_below_threshold_is_ignored()
    {
        long mono = 0;
        var detector = new ClockDriftDetector(() => mono);
        var first = new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc);

        detector.Sample(first, TimeSpan.FromSeconds(60));

        mono += 2000;
        var next = first.AddSeconds(3); // 1 second of drift, well below 60s threshold

        detector.Sample(next, TimeSpan.FromSeconds(60)).Should().BeNull();
    }

    [Fact]
    public void Reset_clears_history()
    {
        long mono = 0;
        var detector = new ClockDriftDetector(() => mono);
        var first = new DateTime(2026, 6, 9, 10, 0, 0, DateTimeKind.Utc);

        detector.Sample(first, TimeSpan.FromSeconds(60));
        detector.Reset();
        detector.Sample(first.AddHours(1), TimeSpan.FromSeconds(60)).Should().BeNull();
    }
}
