using StopMe.Core.ValueObjects;

namespace StopMe.Core.Abstractions;

// Centralises every "is this mutation allowed while a strict session is active?" rule.
// Pure logic — takes the current snapshot of active sessions and a target, returns a
// decision. Callers (IPC handlers, UI) consult it before performing the action and may
// record a BypassEvent when denied.
public interface IStrictModeGuard
{
    StrictModeDecision CanMutateBlocklist(Guid blocklistId, IReadOnlyList<ActiveSessionInstance> activeSessions);

    StrictModeDecision CanDeleteBlocklist(Guid blocklistId, IReadOnlyList<ActiveSessionInstance> activeSessions);

    StrictModeDecision CanMutateSession(Guid sessionId, IReadOnlyList<ActiveSessionInstance> activeSessions);
}
