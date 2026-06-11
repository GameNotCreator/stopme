using StopMe.Core.Enums;

namespace StopMe.Core.Recurrence;

public sealed class RecurrencePattern
{
    public required RecurrenceFrequency Frequency { get; init; }

    public int Interval { get; init; } = 1;

    public IReadOnlySet<DayOfWeek>? ByDay { get; init; }

    public DateTime? UntilUtc { get; init; }

    public int? Count { get; init; }

    public bool IsActiveOn(DateOnly date, DateOnly anchor)
    {
        if (UntilUtc.HasValue && date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) > UntilUtc.Value)
            return false;

        if (date < anchor)
            return false;

        var matches = Frequency switch
        {
            RecurrenceFrequency.Daily => DaysBetween(anchor, date) % Interval == 0,
            RecurrenceFrequency.Weekly => MatchesWeekly(date, anchor),
            _ => false,
        };

        if (!matches) return false;
        if (Count is null) return true;

        // Enforce COUNT: walk from anchor day-by-day, counting matching days,
        // and verify the requested date is within the first `Count` of them.
        // Loop bound = (Count * step) + safety margin; well-bounded for any
        // realistic recurrence.
        return CountReachedBy(date, anchor, Count.Value);
    }

    private bool CountReachedBy(DateOnly target, DateOnly anchor, int count)
    {
        // Linear walk. For typical COUNT values (<= ~100) this is fast and
        // always correct, including BYDAY interactions in weekly recurrence
        // and INTERVAL skips in daily recurrence.
        var occurrence = 0;
        var maxDays = count * 7 * Math.Max(1, Interval) + 366;
        var d = anchor;
        for (var i = 0; i <= maxDays; i++, d = d.AddDays(1))
        {
            if (UntilUtc.HasValue && d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc) > UntilUtc.Value)
                return false;
            if (!OccursOn(d, anchor)) continue;

            occurrence++;
            if (occurrence > count) return false;
            if (d == target) return true;
        }
        return false;
    }

    // Same body as the entry-point switch but WITHOUT the COUNT recursion.
    private bool OccursOn(DateOnly date, DateOnly anchor)
    {
        if (date < anchor) return false;
        return Frequency switch
        {
            RecurrenceFrequency.Daily => DaysBetween(anchor, date) % Interval == 0,
            RecurrenceFrequency.Weekly => MatchesWeekly(date, anchor),
            _ => false,
        };
    }

    private bool MatchesWeekly(DateOnly date, DateOnly anchor)
    {
        var allowedDays = ByDay ?? new HashSet<DayOfWeek> { anchor.DayOfWeek };
        if (!allowedDays.Contains(date.DayOfWeek))
            return false;

        var anchorWeekStart = StartOfIsoWeek(anchor);
        var dateWeekStart = StartOfIsoWeek(date);
        var weeksDiff = DaysBetween(anchorWeekStart, dateWeekStart) / 7;
        return weeksDiff >= 0 && weeksDiff % Interval == 0;
    }

    private static int DaysBetween(DateOnly from, DateOnly to) =>
        to.DayNumber - from.DayNumber;

    private static DateOnly StartOfIsoWeek(DateOnly date)
    {
        int diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-diff);
    }
}
