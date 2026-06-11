using FluentAssertions;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.ValueObjects;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class StatsAggregatorTests
{
    private readonly StatsAggregator _agg = new();

    [Fact]
    public void Aggregates_summary_from_usage_events()
    {
        var window = new StatsWindow(
            new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc));

        var events = new[]
        {
            new UsageEvent { Type = UsageEventType.WebsiteBlocked, DisplayLabel = "youtube.com", OccurredAtUtc = new(2026, 6, 5, 10, 0, 0, DateTimeKind.Utc) },
            new UsageEvent { Type = UsageEventType.WebsiteBlocked, DisplayLabel = "youtube.com", OccurredAtUtc = new(2026, 6, 6, 11, 0, 0, DateTimeKind.Utc) },
            new UsageEvent { Type = UsageEventType.WebsiteBlocked, DisplayLabel = "tiktok.com", OccurredAtUtc = new(2026, 6, 7, 9, 0, 0, DateTimeKind.Utc) },
            new UsageEvent { Type = UsageEventType.AppBlocked,     DisplayLabel = "steam.exe",  OccurredAtUtc = new(2026, 6, 7, 9, 5, 0, DateTimeKind.Utc) },
        };

        var sessions = new[]
        {
            new FocusSession { StartAtUtc = new(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc), EndAtUtc = new(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc) },
            new FocusSession { StartAtUtc = new(2026, 6, 6, 14, 0, 0, DateTimeKind.Utc), EndAtUtc = new(2026, 6, 6, 16, 0, 0, DateTimeKind.Utc) },
        };

        var snapshot = _agg.Aggregate(window, events, sessions, bypassCount: 2);

        snapshot.Summary.SessionsCompleted.Should().Be(2);
        snapshot.Summary.FocusMinutes.Should().Be(60 + 120);
        snapshot.Summary.WebsiteBlockCount.Should().Be(3);
        snapshot.Summary.AppBlockCount.Should().Be(1);
        snapshot.Summary.BypassAttemptCount.Should().Be(2);
    }

    [Fact]
    public void Top_distractions_groups_by_label_and_kind()
    {
        var window = new StatsWindow(
            new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc));

        var events = new[]
        {
            new UsageEvent { Type = UsageEventType.WebsiteBlocked, DisplayLabel = "youtube.com", OccurredAtUtc = new(2026, 6, 5, 10, 0, 0, DateTimeKind.Utc) },
            new UsageEvent { Type = UsageEventType.WebsiteBlocked, DisplayLabel = "youtube.com", OccurredAtUtc = new(2026, 6, 5, 11, 0, 0, DateTimeKind.Utc) },
            new UsageEvent { Type = UsageEventType.WebsiteBlocked, DisplayLabel = "tiktok.com", OccurredAtUtc = new(2026, 6, 7, 9, 0, 0, DateTimeKind.Utc) },
            new UsageEvent { Type = UsageEventType.AppBlocked, DisplayLabel = "steam.exe", OccurredAtUtc = new(2026, 6, 7, 9, 5, 0, DateTimeKind.Utc) },
            new UsageEvent { Type = UsageEventType.AppBlocked, DisplayLabel = "steam.exe", OccurredAtUtc = new(2026, 6, 7, 9, 10, 0, DateTimeKind.Utc) },
        };

        var snapshot = _agg.Aggregate(window, events, Array.Empty<FocusSession>(), bypassCount: 0);

        snapshot.TopDistractions.Should().HaveCount(3);
        snapshot.TopDistractions[0].Label.Should().Be("youtube.com");
        snapshot.TopDistractions[0].BlockCount.Should().Be(2);
        snapshot.TopDistractions[0].Kind.Should().Be("Website");
        snapshot.TopDistractions.Should().Contain(d => d.Label == "steam.exe" && d.Kind == "App" && d.BlockCount == 2);
    }

    [Fact]
    public void Top_distractions_skips_anonymous_events()
    {
        var window = new StatsWindow(
            new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc));

        var events = new[]
        {
            new UsageEvent { Type = UsageEventType.WebsiteBlocked, DisplayLabel = null, DomainHash = "abc", OccurredAtUtc = new(2026, 6, 5, 10, 0, 0, DateTimeKind.Utc) },
            new UsageEvent { Type = UsageEventType.WebsiteBlocked, DisplayLabel = "youtube.com", OccurredAtUtc = new(2026, 6, 5, 11, 0, 0, DateTimeKind.Utc) },
        };

        var snapshot = _agg.Aggregate(window, events, Array.Empty<FocusSession>(), bypassCount: 0);

        snapshot.Summary.WebsiteBlockCount.Should().Be(2);
        snapshot.TopDistractions.Should().ContainSingle().Which.Label.Should().Be("youtube.com");
    }

    [Fact]
    public void Productive_streak_is_longest_consecutive_run_of_completed_sessions()
    {
        var window = new StatsWindow(
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc));

        var sessions = new[]
        {
            // Streak: 1-2-3 (3 days), then gap on 4, then 5-6-7 (3 days)
            new FocusSession { StartAtUtc = new(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc), EndAtUtc = new(2026, 6, 1, 9, 0, 0, DateTimeKind.Utc) },
            new FocusSession { StartAtUtc = new(2026, 6, 2, 8, 0, 0, DateTimeKind.Utc), EndAtUtc = new(2026, 6, 2, 9, 0, 0, DateTimeKind.Utc) },
            new FocusSession { StartAtUtc = new(2026, 6, 3, 8, 0, 0, DateTimeKind.Utc), EndAtUtc = new(2026, 6, 3, 9, 0, 0, DateTimeKind.Utc) },
            new FocusSession { StartAtUtc = new(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc), EndAtUtc = new(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc) },
            new FocusSession { StartAtUtc = new(2026, 6, 6, 8, 0, 0, DateTimeKind.Utc), EndAtUtc = new(2026, 6, 6, 9, 0, 0, DateTimeKind.Utc) },
            new FocusSession { StartAtUtc = new(2026, 6, 7, 8, 0, 0, DateTimeKind.Utc), EndAtUtc = new(2026, 6, 7, 9, 0, 0, DateTimeKind.Utc) },
        };

        var snapshot = _agg.Aggregate(window, Array.Empty<UsageEvent>(), sessions, bypassCount: 0);

        snapshot.Summary.ProductiveDayStreak.Should().Be(3);
    }

    [Fact]
    public void Daily_buckets_cover_every_day_in_window_even_when_empty()
    {
        var window = new StatsWindow(
            new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc));

        var snapshot = _agg.Aggregate(window, Array.Empty<UsageEvent>(), Array.Empty<FocusSession>(), bypassCount: 0);

        snapshot.Daily.Should().HaveCount(3);
        snapshot.Daily.Select(d => d.Date.Day).Should().Equal(5, 6, 7);
        snapshot.Daily.Should().OnlyContain(d => d.FocusMinutes == 0 && d.WebsiteBlockCount == 0 && d.AppBlockCount == 0);
    }
}
