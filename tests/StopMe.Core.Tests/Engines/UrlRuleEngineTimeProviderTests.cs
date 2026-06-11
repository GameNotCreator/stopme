using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class UrlRuleEngineTimeProviderTests
{
    [Fact]
    public void RemainingSeconds_uses_injected_timeprovider()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero));
        var engine = new UrlRuleEngine(clock);

        var rule = new CompiledRule
        {
            RuleId = Guid.NewGuid(),
            BlocklistId = Guid.NewGuid(),
            Type = RuleType.Domain,
            Value = "youtube.com",
            SourceMode = BlocklistMode.Blacklist,
            SessionEndUtc = clock.GetUtcNow().UtcDateTime.AddMinutes(30),
            SessionName = "Focus",
            SessionId = Guid.NewGuid(),
        };

        var decision = engine.Evaluate("https://youtube.com/watch", new[] { rule });
        decision.IsBlocked.Should().BeTrue();
        decision.RemainingSeconds.Should().Be(30 * 60);

        // Advance time without touching DateTime.UtcNow.
        clock.Advance(TimeSpan.FromMinutes(10));
        var later = engine.Evaluate("https://youtube.com/watch", new[] { rule });
        later.RemainingSeconds.Should().Be(20 * 60);
    }
}
