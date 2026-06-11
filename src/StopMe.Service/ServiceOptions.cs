namespace StopMe.Service;

public sealed class ServiceOptions
{
    public const string SectionName = "Service";

    public int TickIntervalSeconds { get; set; } = 2;

    public int GracefulCloseTimeoutSeconds { get; set; } = 5;

    public int MaxConcurrentPipeConnections { get; set; } = 4;

    public string? DataDirectoryOverride { get; set; }

    public string? PipeNameOverride { get; set; }
}
