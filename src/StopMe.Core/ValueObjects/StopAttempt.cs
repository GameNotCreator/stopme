namespace StopMe.Core.ValueObjects;

public sealed class StopAttempt
{
    public bool IsAllowed { get; init; }

    public string? DenialReason { get; init; }

    public static StopAttempt Allowed { get; } = new() { IsAllowed = true };

    public static StopAttempt Denied(string reason) => new() { IsAllowed = false, DenialReason = reason };
}
