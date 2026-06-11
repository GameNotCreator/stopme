using FluentAssertions;
using StopMe.Core.Engines;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class UrlMatcherTests
{
    [Theory]
    [InlineData("facebook.com", "https://facebook.com/", true)]
    [InlineData("facebook.com", "https://www.facebook.com/", true)]
    [InlineData("facebook.com", "https://m.facebook.com/", true)]
    [InlineData("facebook.com", "https://api.m.facebook.com/", true)]
    [InlineData("facebook.com", "https://facebookmail.com/", false)]
    [InlineData("facebook.com", "https://notfacebook.com/", false)]
    [InlineData("youtube.com", "https://youtube.com/watch?v=abc", true)]
    [InlineData("WWW.YouTube.com", "https://youtube.com/", true)]
    public void Domain_match_handles_subdomains_and_www_stripping(string rule, string url, bool expected)
    {
        var n = UrlNormalizer.Normalize(url);
        UrlMatcher.MatchesDomain(rule, n).Should().Be(expected);
    }

    [Theory]
    [InlineData("reddit.com/r/funny", "https://reddit.com/r/funny", true)]
    [InlineData("reddit.com/r/funny", "https://reddit.com/r/funny/", true)]
    [InlineData("reddit.com/r/funny", "https://reddit.com/r/funny/top", true)]
    [InlineData("reddit.com/r/funny", "https://www.reddit.com/r/funny/top", true)]
    [InlineData("reddit.com/r/funny", "https://reddit.com/r/news", false)]
    [InlineData("reddit.com/r/funny", "https://reddit.com/r/funnyish", false)]
    [InlineData("google.com/search?q=cats", "https://google.com/search?q=cats", true)]
    [InlineData("google.com/search?q=cats", "https://google.com/search?q=dogs", false)]
    public void Exact_url_match_supports_path_prefix_when_no_query(string rule, string url, bool expected)
    {
        var n = UrlNormalizer.Normalize(url);
        UrlMatcher.MatchesExactUrl(rule, n).Should().Be(expected);
    }

    [Theory]
    [InlineData("*.youtube.com", "https://www.youtube.com/watch", true)]
    [InlineData("*.youtube.com", "https://music.youtube.com/", true)]
    [InlineData("*.youtube.com", "https://youtube.com/watch", true)]      // apex tolerance
    [InlineData("*.youtube.com", "https://youtubex.com/", false)]
    [InlineData("google.com/*q=*game*", "https://google.com/search?q=cool+game+tonight", true)]
    [InlineData("google.com/*q=*game*", "https://google.com/search?q=cats", false)]
    [InlineData("*.*", "https://anything.com/foo", true)]
    [InlineData("*", "https://anything.com/foo", true)]
    public void Wildcard_match_handles_host_and_path_globs(string rule, string url, bool expected)
    {
        var n = UrlNormalizer.Normalize(url);
        UrlMatcher.MatchesWildcard(rule, n).Should().Be(expected);
    }
}
