using FluentAssertions;
using StopMe.Core.Engines;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class PasswordHasherPbkdfTests
{
    [Fact]
    public void Hash_round_trips_via_verify()
    {
        var hash = PasswordHasher.Hash("correct horse battery staple");
        PasswordHasher.Verify("correct horse battery staple", hash).Should().BeTrue();
        PasswordHasher.Verify("wrong", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_format_is_versioned_and_self_describing()
    {
        var hash = PasswordHasher.Hash("hello", iterations: 10_000);
        hash.Should().StartWith("pbkdf2-sha512$v=1$i=10000$salt=");
        hash.Split('$').Should().HaveCount(5);
    }

    [Fact]
    public void Two_hashes_of_the_same_password_differ()
    {
        var a = PasswordHasher.Hash("hello");
        var b = PasswordHasher.Hash("hello");
        a.Should().NotBe(b, because: "salts are random per call");
        PasswordHasher.Verify("hello", a).Should().BeTrue();
        PasswordHasher.Verify("hello", b).Should().BeTrue();
    }

    [Fact]
    public void Legacy_sha256_hash_is_rejected_and_detected()
    {
        // Legacy unsalted SHA-256 hex for "letmein"
        var legacy = "8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918".ToLowerInvariant();
        PasswordHasher.Verify("letmein", legacy).Should().BeFalse(
            because: "we refuse to validate against the weak placeholder so users must reset");
        PasswordHasher.IsLegacyHash(legacy).Should().BeTrue();
        PasswordHasher.IsLegacyHash(PasswordHasher.Hash("foo")).Should().BeFalse();
    }

    [Fact]
    public void Verify_returns_false_for_missing_arguments()
    {
        PasswordHasher.Verify(null, "anything").Should().BeFalse();
        PasswordHasher.Verify("anything", null).Should().BeFalse();
        PasswordHasher.Verify(string.Empty, "anything").Should().BeFalse();
    }

    [Fact]
    public void Verify_returns_false_for_malformed_pbkdf2_envelope()
    {
        PasswordHasher.Verify("hello", "pbkdf2-sha512$v=99$i=1000$salt=AAA$hash=BBB").Should().BeFalse();
        PasswordHasher.Verify("hello", "pbkdf2-sha512$v=1$i=abc$salt=AAA$hash=BBB").Should().BeFalse();
    }
}
