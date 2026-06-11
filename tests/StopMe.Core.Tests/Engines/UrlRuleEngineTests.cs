using FluentAssertions;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using StopMe.Core.Tests.Fakes;
using StopMe.Core.ValueObjects;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class UrlRuleEngineTests
{
    private readonly UrlRuleEngine _engine = new();

    [Fact]
    public void Domain_rule_blocks_subdomain()
    {
        var rules = new[] { TestData.Rule(RuleType.Domain, "facebook.com") };
        var decision = _engine.Evaluate("https://m.facebook.com/feed", rules);
        decision.IsBlocked.Should().BeTrue();
        decision.MatchedRuleType.Should().Be(RuleType.Domain);
    }

    [Fact]
    public void Url_rule_blocks_path_prefix()
    {
        var rules = new[] { TestData.Rule(RuleType.Url, "reddit.com/r/funny") };
        var decision = _engine.Evaluate("https://reddit.com/r/funny/top", rules);
        decision.IsBlocked.Should().BeTrue();
        decision.MatchedRuleType.Should().Be(RuleType.Url);
    }

    [Fact]
    public void Wildcard_rule_blocks_pattern_match()
    {
        var rules = new[] { TestData.Rule(RuleType.Wildcard, "*.youtube.com") };
        var decision = _engine.Evaluate("https://music.youtube.com/playlist", rules);
        decision.IsBlocked.Should().BeTrue();
        decision.MatchedRuleType.Should().Be(RuleType.Wildcard);
    }

    [Fact]
    public void Exception_url_overrides_domain_block()
    {
        var rules = new[]
        {
            TestData.Rule(RuleType.Domain, "tiktok.com"),
            TestData.Rule(RuleType.ExceptionUrl, "tiktok.com/legal/privacy"),
        };
        var decision = _engine.Evaluate("https://tiktok.com/legal/privacy", rules);
        decision.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void Exception_domain_overrides_wildcard_block()
    {
        var rules = new[]
        {
            TestData.Rule(RuleType.Wildcard, "*.tiktok.com"),
            TestData.Rule(RuleType.ExceptionDomain, "vm.tiktok.com"),
        };
        var decision = _engine.Evaluate("https://vm.tiktok.com/abc", rules);
        decision.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void Exception_wildcard_overrides_block()
    {
        var rules = new[]
        {
            TestData.Rule(RuleType.Domain, "google.com"),
            TestData.Rule(RuleType.ExceptionWildcard, "*.google.com/calendar*"),
        };
        var decision = _engine.Evaluate("https://www.google.com/calendar/u/0", rules);
        decision.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void Allowlist_mode_blocks_url_not_in_list()
    {
        var blocklistId = Guid.NewGuid();
        var rules = new[]
        {
            TestData.Rule(RuleType.Domain, "github.com", BlocklistMode.Allowlist, blocklistId),
            TestData.Rule(RuleType.Domain, "stackoverflow.com", BlocklistMode.Allowlist, blocklistId),
        };
        var decision = _engine.Evaluate("https://youtube.com/", rules);
        decision.IsBlocked.Should().BeTrue();
        decision.Reason.Should().Contain("allowlist");
    }

    [Fact]
    public void Allowlist_mode_allows_url_in_list()
    {
        var blocklistId = Guid.NewGuid();
        var rules = new[]
        {
            TestData.Rule(RuleType.Domain, "github.com", BlocklistMode.Allowlist, blocklistId),
            TestData.Rule(RuleType.Domain, "stackoverflow.com", BlocklistMode.Allowlist, blocklistId),
        };
        var decision = _engine.Evaluate("https://github.com/anthropics", rules);
        decision.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void Allowlist_mode_yields_to_exception_even_when_url_not_in_list()
    {
        var blocklistId = Guid.NewGuid();
        var rules = new[]
        {
            TestData.Rule(RuleType.Domain, "github.com", BlocklistMode.Allowlist, blocklistId),
            TestData.Rule(RuleType.ExceptionUrl, "docs.example.com/specs"),
        };
        var decision = _engine.Evaluate("https://docs.example.com/specs/v1", rules);
        decision.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void FullInternet_blocklist_mode_blocks_everything_without_exception()
    {
        var rules = new[]
        {
            TestData.Rule(RuleType.Domain, "anything", BlocklistMode.FullInternet),
        };
        var decision = _engine.Evaluate("https://example.org/foo", rules);
        decision.IsBlocked.Should().BeTrue();
        decision.MatchedRuleType.Should().Be(RuleType.FullInternet);
    }

    [Fact]
    public void FullInternet_yields_to_exception_for_allowed_url()
    {
        var rules = new[]
        {
            TestData.Rule(RuleType.FullInternet, "*"),
            TestData.Rule(RuleType.ExceptionDomain, "docs.google.com"),
        };
        var decision = _engine.Evaluate("https://docs.google.com/edit/x", rules);
        decision.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void Allow_when_no_rule_matches()
    {
        var rules = new[] { TestData.Rule(RuleType.Domain, "tiktok.com") };
        var decision = _engine.Evaluate("https://wikipedia.org/wiki/Cat", rules);
        decision.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public void Decision_carries_session_metadata()
    {
        var endUtc = DateTime.UtcNow.AddMinutes(40);
        var sessionId = Guid.NewGuid();
        var rules = new[]
        {
            TestData.Rule(RuleType.Domain, "youtube.com",
                sessionId: sessionId,
                sessionName: "Study Session",
                sessionEndUtc: endUtc),
        };
        var decision = _engine.Evaluate("https://youtube.com/watch", rules);
        decision.IsBlocked.Should().BeTrue();
        decision.SessionId.Should().Be(sessionId);
        decision.SessionName.Should().Be("Study Session");
        decision.RemainingSeconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Invalid_url_is_allowed_silently()
    {
        var rules = new[] { TestData.Rule(RuleType.Domain, "facebook.com") };
        var decision = _engine.Evaluate("not a url", rules);
        decision.IsBlocked.Should().BeFalse();
    }
}
