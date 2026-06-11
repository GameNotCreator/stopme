namespace StopMe.LocalApi.Dtos;

public sealed class BlocklistDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Mode { get; init; }       // BlocklistMode.ToString()
    public required bool IsEnabled { get; init; }
    public required IReadOnlyList<BlockRuleDto> Rules { get; init; }
}

public sealed class BlockRuleDto
{
    public required Guid Id { get; init; }
    public required string Type { get; init; }       // RuleType.ToString()
    public required string Value { get; init; }
    public required bool IsEnabled { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateBlocklistRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Mode { get; init; }
    public IReadOnlyList<CreateRuleRequest>? Rules { get; init; }
}

public sealed class CreateRuleRequest
{
    public required string Type { get; init; }
    public required string Value { get; init; }
    public string? Notes { get; init; }
}

public sealed class UpdateBlocklistRequest
{
    public required Guid Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Mode { get; init; }
    public bool? IsEnabled { get; init; }
}

public sealed class DeleteBlocklistRequest
{
    public required Guid Id { get; init; }
}
