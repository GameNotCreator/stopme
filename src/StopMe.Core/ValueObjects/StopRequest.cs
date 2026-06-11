using StopMe.Core.Enums;

namespace StopMe.Core.ValueObjects;

public sealed class StopRequest
{
    public required StopReason Reason { get; init; }

    public bool UserConfirmed { get; init; }

    public string? Password { get; init; }

    public string? TypedText { get; init; }
}
