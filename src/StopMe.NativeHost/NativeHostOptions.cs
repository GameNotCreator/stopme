namespace StopMe.NativeHost;

public sealed class NativeHostOptions
{
    public required string DataDirectory { get; init; }

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(5);

    public static NativeHostOptions Resolve()
    {
        var env = Environment.GetEnvironmentVariable("STOPME_DATA_DIR");
        var dir = !string.IsNullOrWhiteSpace(env)
            ? Path.GetFullPath(env)
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "StopMe");

        return new NativeHostOptions { DataDirectory = dir };
    }
}
