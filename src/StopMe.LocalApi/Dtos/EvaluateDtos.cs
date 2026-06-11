namespace StopMe.LocalApi.Dtos;

public sealed class EvaluateUrlRequest
{
    public required string Url { get; init; }
    public string? Browser { get; init; }
    public int? TabId { get; init; }
}

public sealed class EvaluateUrlResponse
{
    public required bool Blocked { get; init; }
    public string? Reason { get; init; }
    public Guid? MatchedRuleId { get; init; }
    public string? MatchedRuleType { get; init; }
    public Guid? SessionId { get; init; }
    public string? SessionName { get; init; }
    public int? RemainingSeconds { get; init; }
}

public sealed class EvaluateAppRequest
{
    public required int ProcessId { get; init; }
    public required string ProcessName { get; init; }
    public string? ExecutablePath { get; init; }
    public string? MainWindowTitle { get; init; }
    public string? PackageFamilyName { get; init; }
    public bool IsMicrosoftStoreApp { get; init; }
}

public sealed class EvaluateAppResponse
{
    public required bool Blocked { get; init; }
    public string? Reason { get; init; }
    public Guid? MatchedRuleId { get; init; }
    public string? MatchedRuleType { get; init; }
    public Guid? SessionId { get; init; }
    public string? SessionName { get; init; }
}
