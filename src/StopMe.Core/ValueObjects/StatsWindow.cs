using StopMe.Core.Enums;

namespace StopMe.Core.ValueObjects;

public sealed record StatsWindow(DateTime FromUtc, DateTime ToUtc)
{
    public TimeSpan Duration => ToUtc - FromUtc;

    // Clamped upper bound for the "AllTime" preset. The old implementation used
    // DateTime.MinValue (year 1), and StatsAggregator's day-bucket enumerator
    // walked every day in the window — about 739k days, freezing the page.
    // 365 days matches the actual time-span most users will ever generate
    // events for, while keeping the UI responsive.
    public const int AllTimeMaxDays = 365;

    public static StatsWindow From(StatsPeriod period, DateTime nowUtc)
    {
        var today = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        return period switch
        {
            StatsPeriod.Today => new StatsWindow(today, today.AddDays(1)),
            StatsPeriod.Last7Days => new StatsWindow(today.AddDays(-6), today.AddDays(1)),
            StatsPeriod.Last30Days => new StatsWindow(today.AddDays(-29), today.AddDays(1)),
            StatsPeriod.AllTime => new StatsWindow(today.AddDays(-(AllTimeMaxDays - 1)), today.AddDays(1)),
            _ => throw new ArgumentOutOfRangeException(nameof(period)),
        };
    }
}
