using StopMe.Core.Enums;

namespace StopMe.Core.ValueObjects;

public sealed record StatsWindow(DateTime FromUtc, DateTime ToUtc)
{
    public TimeSpan Duration => ToUtc - FromUtc;

    public static StatsWindow From(StatsPeriod period, DateTime nowUtc)
    {
        var today = new DateTime(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, DateTimeKind.Utc);
        return period switch
        {
            StatsPeriod.Today => new StatsWindow(today, today.AddDays(1)),
            StatsPeriod.Last7Days => new StatsWindow(today.AddDays(-6), today.AddDays(1)),
            StatsPeriod.Last30Days => new StatsWindow(today.AddDays(-29), today.AddDays(1)),
            StatsPeriod.AllTime => new StatsWindow(DateTime.MinValue.ToUniversalTime(), today.AddDays(1)),
            _ => throw new ArgumentOutOfRangeException(nameof(period)),
        };
    }
}
