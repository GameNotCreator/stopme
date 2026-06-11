namespace StopMe.Core.ValueObjects;

public sealed class StrictModeDecision
{
    public bool IsAllowed { get; init; }
    public string? DenialReason { get; init; }
    public Guid? BlockingSessionId { get; init; }

    public static StrictModeDecision Allowed { get; } = new() { IsAllowed = true };

    public static StrictModeDecision Denied(string reason, Guid? blockingSessionId = null) =>
        new()
        {
            IsAllowed = false,
            DenialReason = reason,
            BlockingSessionId = blockingSessionId,
        };
}
