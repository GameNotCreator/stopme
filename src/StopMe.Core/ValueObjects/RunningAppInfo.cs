namespace StopMe.Core.ValueObjects;

public sealed class RunningAppInfo
{
    public required int ProcessId { get; init; }

    public required string ProcessName { get; init; }

    public string? ExecutablePath { get; init; }

    public string? MainWindowTitle { get; init; }

    public string? PackageFamilyName { get; init; }

    public bool IsMicrosoftStoreApp { get; init; }
}
