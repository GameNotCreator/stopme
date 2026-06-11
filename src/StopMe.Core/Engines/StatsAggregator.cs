using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Engines;

public sealed class StatsAggregator : IStatsAggregator
{
    public StatsSnapshot Aggregate(
        StatsWindow window,
        IReadOnlyList<UsageEvent> usageEvents,
        IReadOnlyList<FocusSession> completedSessions,
        int bypassCount,
        int topDistractionsLimit = 5)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(usageEvents);
        ArgumentNullException.ThrowIfNull(completedSessions);
        if (topDistractionsLimit <= 0) topDistractionsLimit = 5;

        var websiteEvents = usageEvents.Where(e => e.Type == UsageEventType.WebsiteBlocked).ToList();
        var appEvents = usageEvents.Where(e => e.Type == UsageEventType.AppBlocked).ToList();

        var sessionsByDay = completedSessions
            .GroupBy(s => DateOnly.FromDateTime(s.EndAtUtc))
            .ToDictionary(g => g.Key, g => g.ToList());

        var websiteByDay = websiteEvents
            .GroupBy(e => DateOnly.FromDateTime(e.OccurredAtUtc))
            .ToDictionary(g => g.Key, g => g.Count());

        var appByDay = appEvents
            .GroupBy(e => DateOnly.FromDateTime(e.OccurredAtUtc))
            .ToDictionary(g => g.Key, g => g.Count());

        var daily = EnumerateDays(window)
            .Select(d => new DailyBucket
            {
                Date = d,
                FocusMinutes = sessionsByDay.TryGetValue(d, out var sList)
                    ? (int)Math.Round(sList.Sum(s => (s.EndAtUtc - s.StartAtUtc).TotalMinutes))
                    : 0,
                WebsiteBlockCount = websiteByDay.GetValueOrDefault(d),
                AppBlockCount = appByDay.GetValueOrDefault(d),
                SessionsCompleted = sessionsByDay.TryGetValue(d, out var s2) ? s2.Count : 0,
            })
            .ToArray();

        var summary = new StatsSummary
        {
            SessionsCompleted = completedSessions.Count,
            FocusMinutes = (int)Math.Round(completedSessions.Sum(s => (s.EndAtUtc - s.StartAtUtc).TotalMinutes)),
            WebsiteBlockCount = websiteEvents.Count,
            AppBlockCount = appEvents.Count,
            BypassAttemptCount = bypassCount,
            ProductiveDayStreak = ComputeStreak(daily),
        };

        var topDistractions = usageEvents
            .Where(e => e.Type is UsageEventType.WebsiteBlocked or UsageEventType.AppBlocked)
            .Where(e => !string.IsNullOrWhiteSpace(e.DisplayLabel))
            .GroupBy(e => new { e.Type, Label = e.DisplayLabel! })
            .Select(g => new TopDistraction
            {
                Label = g.Key.Label,
                BlockCount = g.Count(),
                Kind = g.Key.Type == UsageEventType.WebsiteBlocked ? "Website" : "App",
            })
            .OrderByDescending(d => d.BlockCount)
            .Take(topDistractionsLimit)
            .ToArray();

        return new StatsSnapshot
        {
            Window = window,
            Summary = summary,
            Daily = daily,
            TopDistractions = topDistractions,
        };
    }

    private static IEnumerable<DateOnly> EnumerateDays(StatsWindow window)
    {
        var start = DateOnly.FromDateTime(window.FromUtc);
        var end = DateOnly.FromDateTime(window.ToUtc.AddTicks(-1));
        if (end < start) yield break;
        for (var d = start; d <= end; d = d.AddDays(1))
            yield return d;
    }

    // Longest run of consecutive days (ending at today or earlier) where at least one
    // session was completed. Pure post-aggregation pass.
    private static int ComputeStreak(IReadOnlyList<DailyBucket> daily)
    {
        if (daily.Count == 0) return 0;
        int longest = 0;
        int current = 0;
        foreach (var b in daily)
        {
            if (b.SessionsCompleted > 0)
            {
                current++;
                if (current > longest) longest = current;
            }
            else
            {
                current = 0;
            }
        }
        return longest;
    }
}
