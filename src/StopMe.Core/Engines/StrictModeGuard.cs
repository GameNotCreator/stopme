using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;

namespace StopMe.Core.Engines;

public sealed class StrictModeGuard : IStrictModeGuard
{
    public StrictModeDecision CanMutateBlocklist(Guid blocklistId, IReadOnlyList<ActiveSessionInstance> activeSessions)
    {
        ArgumentNullException.ThrowIfNull(activeSessions);
        var blocker = activeSessions.FirstOrDefault(s =>
            s.Mode == SessionMode.Strict && s.BlocklistIds.Contains(blocklistId));
        return blocker is null
            ? StrictModeDecision.Allowed
            : StrictModeDecision.Denied(
                $"Strict session '{blocker.Name}' is using this blocklist until {blocker.EndAtUtc:O}.",
                blocker.SessionId);
    }

    public StrictModeDecision CanDeleteBlocklist(Guid blocklistId, IReadOnlyList<ActiveSessionInstance> activeSessions)
    {
        ArgumentNullException.ThrowIfNull(activeSessions);
        // Deletion is forbidden by ANY active session (strict or soft) — see §13.
        var blocker = activeSessions.FirstOrDefault(s => s.BlocklistIds.Contains(blocklistId));
        return blocker is null
            ? StrictModeDecision.Allowed
            : StrictModeDecision.Denied(
                $"Active session '{blocker.Name}' is using this blocklist.",
                blocker.SessionId);
    }

    public StrictModeDecision CanMutateSession(Guid sessionId, IReadOnlyList<ActiveSessionInstance> activeSessions)
    {
        ArgumentNullException.ThrowIfNull(activeSessions);
        var blocker = activeSessions.FirstOrDefault(s =>
            s.SessionId == sessionId && s.Mode == SessionMode.Strict);
        return blocker is null
            ? StrictModeDecision.Allowed
            : StrictModeDecision.Denied(
                $"Strict session '{blocker.Name}' is active and cannot be edited.",
                blocker.SessionId);
    }
}
