namespace StopMe.LocalApi.Dtos;

public sealed class TemplateDto
{
    public required string Key { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required int DurationMinutes { get; init; }
    public required string Mode { get; init; }
    public required string LockPolicy { get; init; }
    public required IReadOnlyList<TemplateBlocklistDto> Blocklists { get; init; }
    public string? DefaultRecurrenceRule { get; init; }
}

public sealed class TemplateBlocklistDto
{
    public required string Name { get; init; }
    public required string Mode { get; init; }
    public required IReadOnlyList<string> Categories { get; init; }
    public required IReadOnlyList<string> Domains { get; init; }
    public required IReadOnlyList<string> ProcessNames { get; init; }
}

public sealed class TemplatesListResponse
{
    public required IReadOnlyList<TemplateDto> Templates { get; init; }
}

public sealed class TemplateApplyRequest
{
    public required string TemplateKey { get; init; }
    public required DateTime StartAtUtc { get; init; }
}

public sealed class TemplateApplyResponse
{
    public required bool Success { get; init; }
    public Guid? SessionId { get; init; }
    public IReadOnlyList<Guid> BlocklistIds { get; init; } = Array.Empty<Guid>();
    public string? Error { get; init; }
}
