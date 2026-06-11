using FluentAssertions;
using StopMe.Core.Enums;
using StopMe.Core.Recurrence;
using Xunit;

namespace StopMe.Core.Tests.Recurrence;

public class RecurrencePatternTests
{
    [Fact]
    public void Daily_active_every_day_from_anchor()
    {
        var p = new RecurrencePattern { Frequency = RecurrenceFrequency.Daily };
        var anchor = new DateOnly(2026, 6, 1);
        p.IsActiveOn(anchor, anchor).Should().BeTrue();
        p.IsActiveOn(anchor.AddDays(1), anchor).Should().BeTrue();
        p.IsActiveOn(anchor.AddDays(30), anchor).Should().BeTrue();
    }

    [Fact]
    public void Daily_inactive_before_anchor()
    {
        var p = new RecurrencePattern { Frequency = RecurrenceFrequency.Daily };
        var anchor = new DateOnly(2026, 6, 8);
        p.IsActiveOn(anchor.AddDays(-1), anchor).Should().BeFalse();
    }

    [Fact]
    public void Daily_interval_skips_days()
    {
        var p = new RecurrencePattern { Frequency = RecurrenceFrequency.Daily, Interval = 2 };
        var anchor = new DateOnly(2026, 6, 1);
        p.IsActiveOn(anchor, anchor).Should().BeTrue();
        p.IsActiveOn(anchor.AddDays(1), anchor).Should().BeFalse();
        p.IsActiveOn(anchor.AddDays(2), anchor).Should().BeTrue();
    }

    [Fact]
    public void Weekly_byday_filters_days_of_week()
    {
        // Anchor 2026-06-01 = Monday
        var anchor = new DateOnly(2026, 6, 1);
        var weekdays = new HashSet<DayOfWeek>
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        };
        var p = new RecurrencePattern { Frequency = RecurrenceFrequency.Weekly, ByDay = weekdays };

        p.IsActiveOn(anchor, anchor).Should().BeTrue();              // Monday
        p.IsActiveOn(anchor.AddDays(5), anchor).Should().BeFalse();  // Saturday
        p.IsActiveOn(anchor.AddDays(6), anchor).Should().BeFalse();  // Sunday
        p.IsActiveOn(anchor.AddDays(7), anchor).Should().BeTrue();   // next Monday
    }

    [Fact]
    public void Weekly_interval_skips_weeks()
    {
        var anchor = new DateOnly(2026, 6, 1); // Monday
        var p = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.Weekly,
            Interval = 2,
            ByDay = new HashSet<DayOfWeek> { DayOfWeek.Monday },
        };

        p.IsActiveOn(anchor, anchor).Should().BeTrue();
        p.IsActiveOn(anchor.AddDays(7), anchor).Should().BeFalse();
        p.IsActiveOn(anchor.AddDays(14), anchor).Should().BeTrue();
    }

    [Fact]
    public void Until_terminates_recurrence()
    {
        var p = new RecurrencePattern
        {
            Frequency = RecurrenceFrequency.Daily,
            UntilUtc = new DateTime(2026, 6, 10, 0, 0, 0, DateTimeKind.Utc),
        };
        var anchor = new DateOnly(2026, 6, 1);
        p.IsActiveOn(new DateOnly(2026, 6, 10), anchor).Should().BeTrue();
        p.IsActiveOn(new DateOnly(2026, 6, 11), anchor).Should().BeFalse();
    }
}
