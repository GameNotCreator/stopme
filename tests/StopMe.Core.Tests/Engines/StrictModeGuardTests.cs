using FluentAssertions;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class StrictModeGuardTests
{
    private readonly StrictModeGuard _guard = new();

    [Fact]
    public void Mutating_blocklist_not_used_by_strict_session_is_allowed()
    {
        var blocklistId = Guid.NewGuid();
        var decision = _guard.CanMutateBlocklist(blocklistId, Array.Empty<ActiveSessionInstance>());
        decision.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Mutating_blocklist_used_by_strict_session_is_denied()
    {
        var blocklistId = Guid.NewGuid();
        var sessions = new[] { Active("Strict Focus", SessionMode.Strict, blocklistId) };

        var decision = _guard.CanMutateBlocklist(blocklistId, sessions);

        decision.IsAllowed.Should().BeFalse();
        decision.DenialReason.Should().Contain("Strict Focus");
        decision.BlockingSessionId.Should().Be(sessions[0].SessionId);
    }

    [Fact]
    public void Mutating_blocklist_used_by_soft_session_is_allowed()
    {
        var blocklistId = Guid.NewGuid();
        var sessions = new[] { Active("Casual", SessionMode.Soft, blocklistId) };

        _guard.CanMutateBlocklist(blocklistId, sessions).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Deleting_blocklist_used_by_any_active_session_is_denied()
    {
        var blocklistId = Guid.NewGuid();
        var sessions = new[] { Active("Casual", SessionMode.Soft, blocklistId) };

        var decision = _guard.CanDeleteBlocklist(blocklistId, sessions);
        decision.IsAllowed.Should().BeFalse();
        decision.DenialReason.Should().Contain("Casual");
    }

    [Fact]
    public void Deleting_blocklist_referenced_by_no_session_is_allowed()
    {
        var blocklistId = Guid.NewGuid();
        var sessions = new[] { Active("Other", SessionMode.Strict, Guid.NewGuid()) };

        _guard.CanDeleteBlocklist(blocklistId, sessions).IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Mutating_active_strict_session_is_denied()
    {
        var strict = Active("Lockdown", SessionMode.Strict, Guid.NewGuid());
        var decision = _guard.CanMutateSession(strict.SessionId, new[] { strict });
        decision.IsAllowed.Should().BeFalse();
        decision.BlockingSessionId.Should().Be(strict.SessionId);
    }

    [Fact]
    public void Mutating_active_soft_session_is_allowed()
    {
        var soft = Active("Casual", SessionMode.Soft, Guid.NewGuid());
        _guard.CanMutateSession(soft.SessionId, new[] { soft }).IsAllowed.Should().BeTrue();
    }

    private static ActiveSessionInstance Active(string name, SessionMode mode, Guid blocklistId) =>
        new()
        {
            SessionId = Guid.NewGuid(),
            Name = name,
            StartAtUtc = DateTime.UtcNow,
            EndAtUtc = DateTime.UtcNow.AddHours(1),
            Mode = mode,
            LockPolicy = LockPolicy.None,
            BlocklistIds = new[] { blocklistId },
        };
}
