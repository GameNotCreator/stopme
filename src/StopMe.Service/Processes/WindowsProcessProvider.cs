using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using StopMe.Core.ValueObjects;

namespace StopMe.Service.Processes;

public sealed class WindowsProcessProvider : IProcessProvider
{
    private static readonly HashSet<string> ProtectedProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // Critical OS surfaces we never touch. Keep small — only kernel/UI pillars.
        "system", "idle", "registry", "smss", "csrss", "wininit", "winlogon",
        "services", "lsass", "lsaiso", "fontdrvhost", "dwm", "explorer",
        // Self-protection: never terminate the service or its host.
        "stopme.service",
    };

    private readonly ILogger<WindowsProcessProvider> _logger;

    public WindowsProcessProvider(ILogger<WindowsProcessProvider> logger) => _logger = logger;

    public IEnumerable<RunningAppInfo> SnapshotRunning()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcesses();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate processes.");
            yield break;
        }

        foreach (var p in processes)
        {
            using (p)
            {
                if (ProtectedProcessNames.Contains(p.ProcessName))
                    continue;

                RunningAppInfo? info = TryDescribe(p);
                if (info is not null)
                    yield return info;
            }
        }
    }

    private static RunningAppInfo? TryDescribe(Process p)
    {
        try
        {
            return new RunningAppInfo
            {
                ProcessId = p.Id,
                ProcessName = p.ProcessName,
                ExecutablePath = SafeGetExecutablePath(p),
                MainWindowTitle = SafeGetMainWindowTitle(p),
            };
        }
        catch (InvalidOperationException)
        {
            return null; // process exited between enumeration and inspection
        }
        catch (Win32Exception)
        {
            return null; // access denied (protected process)
        }
    }

    private static string? SafeGetExecutablePath(Process p)
    {
        try { return p.MainModule?.FileName; }
        catch (Win32Exception) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (NotSupportedException) { return null; }
    }

    private static string? SafeGetMainWindowTitle(Process p)
    {
        try
        {
            var title = p.MainWindowTitle;
            return string.IsNullOrEmpty(title) ? null : title;
        }
        catch (InvalidOperationException) { return null; }
        catch (Win32Exception) { return null; }
    }
}
