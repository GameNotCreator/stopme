namespace StopMe.App.Core.Services;

public sealed class ServiceConnectionOptions
{
    public string? DataDirectoryOverride { get; set; }

    public string? PipeNameOverride { get; set; }

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(3);

    public string ResolveDataDirectory() =>
        !string.IsNullOrWhiteSpace(DataDirectoryOverride)
            ? DataDirectoryOverride!
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "StopMe");
}
