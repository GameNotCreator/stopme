namespace StopMe.LocalApi.Dtos;

public sealed class SessionListRequest
{
    // Optional filter; null = all enabled sessions.
    public string? StatusFilter { get; init; }
}

public sealed class SessionListItemDto
{
    public required Guid SessionId { get; init; }
    public required string Name { get; init; }
    public required DateTime StartAtUtc { get; init; }
    public required DateTime EndAtUtc { get; init; }
    public required string Status { get; init; }
    public required string Mode { get; init; }
    public required string LockPolicy { get; init; }
    public required bool IsRecurring { get; init; }
    public string? RecurrenceRule { get; init; }
    public string TimeZoneId { get; init; } = "UTC";
    public required IReadOnlyList<Guid> BlocklistIds { get; init; }
}
