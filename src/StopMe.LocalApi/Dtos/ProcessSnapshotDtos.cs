namespace StopMe.LocalApi.Dtos;

public sealed class ProcessSnapshotResponse
{
    public required IReadOnlyList<RunningAppInfoDto> Apps { get; init; }
    public required DateTime CapturedAtUtc { get; init; }
}

public sealed class RunningAppInfoDto
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public string? ExecutablePath { get; init; }
    public string? MainWindowTitle { get; init; }
    public bool HasVisibleWindow { get; init; }
}

public sealed class CreateSessionRequest
{
    public required string Name { get; init; }
    public required DateTime StartAtUtc { get; init; }
    public required DateTime EndAtUtc { get; init; }
    public required string Mode { get; init; }           // SessionMode.ToString()
    public required string LockPolicy { get; init; }     // LockPolicy.ToString()
    public IReadOnlyList<Guid> BlocklistIds { get; init; } = Array.Empty<Guid>();
    public string? RequiredText { get; init; }
    public string? Password { get; init; }
    public bool StartImmediately { get; init; }
}

public sealed class CreateSessionResponse
{
    public required Guid SessionId { get; init; }
    public required bool Started { get; init; }
    public string? Warning { get; init; }
}

public sealed class AddRuleRequest
{
    public required Guid BlocklistId { get; init; }
    public required IReadOnlyList<CreateRuleRequest> Rules { get; init; }
}

public sealed class AddRuleResponse
{
    public required int AddedCount { get; init; }
    public required IReadOnlyList<BlockRuleDto> Rules { get; init; }
}

public sealed class InstalledAppsResponse
{
    public required IReadOnlyList<InstalledAppInfoDto> Apps { get; init; }
    public required DateTime CapturedAtUtc { get; init; }
}

public sealed class InstalledAppInfoDto
{
    public required string DisplayName { get; init; }
    public string? Publisher { get; init; }
    public string? Version { get; init; }
    public string? ExecutablePath { get; init; }
    public string? ProcessName { get; init; }
    public required string Source { get; init; }
}

public sealed class DomainSearchRequest
{
    public required string Query { get; init; }
    public int MaxResults { get; init; } = 12;
}

public sealed class DomainSearchResponse
{
    public required IReadOnlyList<DomainSearchHitDto> Hits { get; init; }
    public required string Provider { get; init; } // "DuckDuckGo" today
}

public sealed class DomainSearchHitDto
{
    public required string Domain { get; init; }
    public required string Title { get; init; }
    public string? Url { get; init; }
}
