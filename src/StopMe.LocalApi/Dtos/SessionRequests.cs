namespace StopMe.LocalApi.Dtos;

public sealed class StartSessionRequest
{
    public required Guid SessionId { get; init; }
}

public sealed class StopSessionRequest
{
    public required Guid SessionId { get; init; }
    public required string Reason { get; init; }
    public bool UserConfirmed { get; init; }
    public string? Password { get; init; }
    public string? TypedText { get; init; }
}

public sealed class StopSessionResponse
{
    public required bool Stopped { get; init; }
    public string? DenialReason { get; init; }
}
