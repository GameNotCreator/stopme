using FluentAssertions;
using StopMe.Core.Enums;
using StopMe.Core.Recurrence;
using Xunit;

namespace StopMe.Core.Tests.Recurrence;

public class RecurrenceParserTests
{
    [Fact]
    public void Parse_daily()
    {
        var p = RecurrenceParser.Parse("FREQ=DAILY");
        p.Frequency.Should().Be(RecurrenceFrequency.Daily);
        p.Interval.Should().Be(1);
        p.ByDay.Should().BeNull();
    }

    [Fact]
    public void Parse_weekly_with_byday()
    {
        var p = RecurrenceParser.Parse("FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR");
        p.Frequency.Should().Be(RecurrenceFrequency.Weekly);
        p.ByDay.Should().BeEquivalentTo(new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
        });
    }

    [Fact]
    public void Parse_accepts_RRULE_prefix()
    {
        var p = RecurrenceParser.Parse("RRULE:FREQ=WEEKLY;BYDAY=MO");
        p.Frequency.Should().Be(RecurrenceFrequency.Weekly);
        p.ByDay.Should().ContainSingle().Which.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public void Parse_interval()
    {
        var p = RecurrenceParser.Parse("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO");
        p.Interval.Should().Be(2);
    }

    [Fact]
    public void Parse_until()
    {
        var p = RecurrenceParser.Parse("FREQ=DAILY;UNTIL=20260615T120000Z");
        p.UntilUtc.Should().Be(new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Parse_count()
    {
        var p = RecurrenceParser.Parse("FREQ=DAILY;COUNT=10");
        p.Count.Should().Be(10);
    }

    [Theory]
    [InlineData("BYDAY=MO")]
    [InlineData("FREQ=MONTHLY")]
    [InlineData("FREQ=WEEKLY;INTERVAL=0")]
    [InlineData("FREQ=WEEKLY;BYDAY=ZZ")]
    public void Parse_rejects_invalid_input(string input)
    {
        Action act = () => RecurrenceParser.Parse(input);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Format_roundtrip()
    {
        var p = RecurrenceParser.Parse("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE,FR");
        var formatted = RecurrenceParser.Format(p);
        formatted.Should().Be("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE,FR");
    }

    [Fact]
    public void TryParse_returns_false_on_error()
    {
        RecurrenceParser.TryParse("garbage", out var _).Should().BeFalse();
    }
}
