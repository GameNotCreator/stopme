using System.Diagnostics;
using System.Runtime.Versioning;

namespace StopMe.Service.Installation;

[SupportedOSPlatform("windows")]
public static class ServiceControl
{
    public const string ServiceName = "StopMeService";
    public const string DisplayName = "StopMe Focus Service";
    public const string Description = "Enforces StopMe focus sessions: blocks distracting websites and applications, exposes the local IPC API.";

    public static async Task<int> InstallAsync(string exePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(exePath);
        if (!File.Exists(exePath))
        {
            Console.Error.WriteLine($"Executable not found: {exePath}");
            return 1;
        }

        var binPath = $"\"{exePath}\"";

        var create = await RunScAsync($"create {ServiceName} binPath= {binPath} start= auto DisplayName= \"{DisplayName}\"").ConfigureAwait(false);
        if (create != 0) return create;

        await RunScAsync($"description {ServiceName} \"{Description}\"").ConfigureAwait(false);
        await RunScAsync($"failure {ServiceName} reset= 86400 actions= restart/60000/restart/120000/restart/300000").ConfigureAwait(false);

        Console.WriteLine($"Installed Windows service '{ServiceName}'. Use `{ServiceName}` with sc.exe or `StopMe.Service.exe start` to start.");
        return 0;
    }

    public static Task<int> UninstallAsync() => RunScAsync($"delete {ServiceName}");

    public static Task<int> StartAsync() => RunScAsync($"start {ServiceName}");

    public static Task<int> StopAsync() => RunScAsync($"stop {ServiceName}");

    public static Task<int> StatusAsync() => RunScAsync($"query {ServiceName}");

    private static async Task<int> RunScAsync(string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Could not launch sc.exe.");

        var stdOut = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        var stdErr = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
        await process.WaitForExitAsync().ConfigureAwait(false);

        if (stdOut.Length > 0) Console.Out.Write(stdOut);
        if (stdErr.Length > 0) Console.Error.Write(stdErr);
        return process.ExitCode;
    }
}
