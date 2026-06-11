using FluentAssertions;
using StopMe.Core.Engines;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class UrlNormalizerTests
{
    [Theory]
    [InlineData("https://YouTube.com/", "youtube.com", "/")]
    [InlineData("http://m.facebook.com/login?id=42", "m.facebook.com", "/login")]
    [InlineData("youtube.com", "youtube.com", "/")]
    [InlineData("youtube.com/watch?v=abc", "youtube.com", "/watch")]
    [InlineData("https://example.com:8443/x", "example.com", "/x")]
    public void Normalize_lowercases_host_and_extracts_path(string raw, string expectedHost, string expectedPath)
    {
        var url = UrlNormalizer.Normalize(raw);
        url.Host.Should().Be(expectedHost);
        url.Path.Should().Be(expectedPath);
    }

    [Fact]
    public void Normalize_preserves_querystring()
    {
        var url = UrlNormalizer.Normalize("https://google.com/search?q=cats");
        url.Query.Should().Be("?q=cats");
    }

    [Fact]
    public void Normalize_keeps_non_default_port()
    {
        var url = UrlNormalizer.Normalize("https://example.com:8443/x");
        url.Port.Should().Be(8443);
    }

    [Fact]
    public void Normalize_strips_default_port()
    {
        var url = UrlNormalizer.Normalize("https://example.com:443/x");
        url.Port.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ftp://example.com")]
    public void Normalize_rejects_unsupported_input(string raw)
    {
        Action act = () => UrlNormalizer.Normalize(raw);
        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void TryNormalize_returns_false_for_invalid_input()
    {
        UrlNormalizer.TryNormalize("not a url at all !!!", out var _).Should().BeFalse();
    }

    [Fact]
    public void Normalize_handles_idn_punycode()
    {
        var url = UrlNormalizer.Normalize("https://bücher.example/");
        url.Host.Should().Contain("xn--");
    }
}
