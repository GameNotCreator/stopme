using FluentAssertions;
using StopMe.Core.Enums;
using StopMe.Core.Recurrence;
using Xunit;

namespace StopMe.Core.Tests.Recurrence;

public class RecurrenceCountEnforcementTests
{
    [Fact]
    public void Daily_with_count_3_fires_anchor_plus_two_more_then_stops()
    {
        var pattern = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.Daily,
            Interval = 1,
            Count = 3,
        };
        var anchor = new DateOnly(2026, 6, 10);

        pattern.IsActiveOn(anchor, anchor).Should().BeTrue();             // #1
        pattern.IsActiveOn(anchor.AddDays(1), anchor).Should().BeTrue();  // #2
        pattern.IsActiveOn(anchor.AddDays(2), anchor).Should().BeTrue();  // #3
        pattern.IsActiveOn(anchor.AddDays(3), anchor).Should().BeFalse(); // count exhausted
        pattern.IsActiveOn(anchor.AddDays(10), anchor).Should().BeFalse();
    }

    [Fact]
    public void Daily_count_with_interval_skips_non_matching_days()
    {
        var pattern = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.Daily,
            Interval = 2,
            Count = 3,
        };
        var anchor = new DateOnly(2026, 6, 10);

        pattern.IsActiveOn(anchor, anchor).Should().BeTrue();             // #1 — anchor
        pattern.IsActiveOn(anchor.AddDays(1), anchor).Should().BeFalse(); // off-interval
        pattern.IsActiveOn(anchor.AddDays(2), anchor).Should().BeTrue();  // #2
        pattern.IsActiveOn(anchor.AddDays(4), anchor).Should().BeTrue();  // #3
        pattern.IsActiveOn(anchor.AddDays(6), anchor).Should().BeFalse(); // count exhausted
    }

    [Fact]
    public void Weekly_with_count_counts_each_BYDAY_match()
    {
        // BYDAY=MO,WE,FR, COUNT=5 — first 5 matching days from anchor (a Wednesday).
        var pattern = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.Weekly,
            Interval = 1,
            ByDay = new HashSet<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday },
            Count = 5,
        };
        var anchor = new DateOnly(2026, 6, 10); // Wednesday

        pattern.IsActiveOn(anchor, anchor).Should().BeTrue();                          // #1 Wed
        pattern.IsActiveOn(anchor.AddDays(2), anchor).Should().BeTrue();               // #2 Fri
        pattern.IsActiveOn(anchor.AddDays(5), anchor).Should().BeTrue();               // #3 Mon (next week)
        pattern.IsActiveOn(anchor.AddDays(7), anchor).Should().BeTrue();               // #4 Wed
        pattern.IsActiveOn(anchor.AddDays(9), anchor).Should().BeTrue();               // #5 Fri
        pattern.IsActiveOn(anchor.AddDays(12), anchor).Should().BeFalse();             // count exhausted
    }

    [Fact]
    public void Pattern_without_count_remains_unbounded()
    {
        var pattern = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.Daily,
            Interval = 1,
        };
        var anchor = new DateOnly(2020, 1, 1);
        pattern.IsActiveOn(anchor.AddDays(10_000), anchor).Should().BeTrue();
    }
}
