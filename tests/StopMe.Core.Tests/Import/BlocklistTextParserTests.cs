using FluentAssertions;
using StopMe.Core.Enums;
using StopMe.Core.Import;
using StopMe.Core.Models;
using Xunit;

namespace StopMe.Core.Tests.Import;

public class BlocklistTextParserTests
{
    [Fact]
    public void Parses_each_line_into_typed_rule()
    {
        var text = """
                   youtube.com
                   instagram.com
                   reddit.com/r/funny
                   *.tiktok.com
                   google.com/*q=*game*
                   allow:docs.google.com
                   allow:reddit.com/r/learnprogramming
                   allow:*.github.com
                   category:Social Media
                   *.*
                   """;
        var parsed = BlocklistTextParser.Parse(text);

        parsed.Should().HaveCount(10);
        parsed[0].Type.Should().Be(RuleType.Domain);
        parsed[0].Value.Should().Be("youtube.com");
        parsed[2].Type.Should().Be(RuleType.Url);
        parsed[3].Type.Should().Be(RuleType.Wildcard);
        parsed[4].Type.Should().Be(RuleType.Wildcard);
        parsed[5].Type.Should().Be(RuleType.ExceptionDomain);
        parsed[5].Value.Should().Be("docs.google.com");
        parsed[6].Type.Should().Be(RuleType.ExceptionUrl);
        parsed[7].Type.Should().Be(RuleType.ExceptionWildcard);
        parsed[8].Type.Should().Be(RuleType.Category);
        parsed[8].Value.Should().Be("Social Media");
        parsed[9].Type.Should().Be(RuleType.FullInternet);
    }

    [Fact]
    public void Ignores_blank_and_comment_lines()
    {
        var text = """
                   # blocklist for studying

                   youtube.com
                   ! exclamation comment style
                   instagram.com
                   """;
        var parsed = BlocklistTextParser.Parse(text);
        parsed.Should().HaveCount(2);
    }

    [Fact]
    public void ParseToBlockRules_attaches_blocklist_id_and_timestamp()
    {
        var blocklistId = Guid.NewGuid();
        var now = new DateTime(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc);
        var rules = BlocklistTextParser.ParseToBlockRules("youtube.com\nfacebook.com", blocklistId, now);
        rules.Should().HaveCount(2);
        rules.Should().OnlyContain(r => r.BlocklistId == blocklistId && r.CreatedAtUtc == now && r.IsEnabled);
    }

    [Fact]
    public void Format_roundtrips_rules()
    {
        var blocklistId = Guid.NewGuid();
        var input = "youtube.com\n*.tiktok.com\nallow:docs.google.com\ncategory:Social Media\n*.*";
        var parsed = BlocklistTextParser.ParseToBlockRules(input, blocklistId, DateTime.UtcNow);
        var output = BlocklistTextParser.Format(parsed);

        var lines = output.Split('\n');
        lines.Should().Contain("youtube.com");
        lines.Should().Contain("*.tiktok.com");
        lines.Should().Contain("allow:docs.google.com");
        lines.Should().Contain("category:Social Media");
        lines.Should().Contain("*.*");
    }

    [Fact]
    public void Format_skips_disabled_rules()
    {
        var rules = new[]
        {
            new BlockRule { Type = RuleType.Domain, Value = "youtube.com", IsEnabled = true },
            new BlockRule { Type = RuleType.Domain, Value = "facebook.com", IsEnabled = false },
        };
        var output = BlocklistTextParser.Format(rules);
        output.Should().Be("youtube.com");
    }
}
