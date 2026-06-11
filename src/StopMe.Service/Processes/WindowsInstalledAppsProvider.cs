using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace StopMe.Service.Processes;

// Three-source installed-apps enumerator.
//
//   1. Uninstall registry keys (HKLM 64 + 32 + HKCU). This is what
//      "Add/Remove Programs" reads — every app installed via an MSI/EXE
//      installer is here.
//   2. App Paths registry key (HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths)
//      — gives us short names like "chrome.exe" mapped to a full path.
//   3. Start Menu .lnk shortcuts (per-user + all-users) — catches portable
//      apps and Microsoft Store stubs that don't always appear in Uninstall.
//
// Spec §4.1 says: "Nom humain (prioritaire dans l'UI) : ressource version de
// l'exe — FileDescription". We do that *opportunistically* once we have a path
// (using FileVersionInfo). Falls back to DisplayName / shortcut target name.
[SupportedOSPlatform("windows")]
public sealed class WindowsInstalledAppsProvider : IInstalledAppsProvider
{
    private static readonly string[] UninstallRoots =
    {
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
        @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
    };

    private static readonly string AppPathsRoot =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths";

    // System / shell / OS components we never want to show — Windows itself
    // installs scores of "Microsoft Visual C++ … Redistributable" entries that
    // aren't user-facing apps the user would ever want to block.
    private static readonly string[] BoringPrefixes =
    {
        "Microsoft Visual C++",
        "Microsoft .NET",
        "Microsoft Edge Update",
        "Microsoft Update Health Tools",
        "Microsoft OneDrive Setup",
        "Windows Driver Package",
        "Microsoft Windows SDK",
        "Microsoft DevToolsService",
        "Microsoft Document Explorer",
        "Active Directory Authentication Library",
        "vs_setup",
        "Windows App SDK",
        "Visual Studio Build Tools",
    };

    private readonly ILogger<WindowsInstalledAppsProvider> _logger;

    public WindowsInstalledAppsProvider(ILogger<WindowsInstalledAppsProvider> logger) =>
        _logger = logger;

    public IReadOnlyList<InstalledAppInfo> EnumerateInstalled()
    {
        var seen = new Dictionary<string, InstalledAppInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var entry in ReadUninstallEntries(Registry.LocalMachine, RegistryView.Registry64))
                Merge(seen, entry);
            foreach (var entry in ReadUninstallEntries(Registry.LocalMachine, RegistryView.Registry32))
                Merge(seen, entry);
            foreach (var entry in ReadUninstallEntries(Registry.CurrentUser, RegistryView.Default))
                Merge(seen, entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Uninstall registry enumeration failed.");
        }

        try
        {
            foreach (var entry in ReadAppPaths(Registry.LocalMachine, RegistryView.Registry64))
                Merge(seen, entry);
            foreach (var entry in ReadAppPaths(Registry.LocalMachine, RegistryView.Registry32))
                Merge(seen, entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "App Paths enumeration failed.");
        }

        try
        {
            foreach (var entry in ReadStartMenuShortcuts())
                Merge(seen, entry);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Start menu enumeration failed.");
        }

        return seen.Values
            .OrderBy(a => a.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void Merge(Dictionary<string, InstalledAppInfo> seen, InstalledAppInfo info)
    {
        // De-dupe by ProcessName (preferred) then DisplayName. App-Paths entries
        // win over Uninstall entries because they give us the cleanest exe path,
        // and shortcut hits are preferred over both when an exe was resolved.
        var key = !string.IsNullOrEmpty(info.ProcessName)
            ? "p:" + info.ProcessName.ToLowerInvariant()
            : "n:" + info.DisplayName.ToLowerInvariant();

        if (!seen.TryGetValue(key, out var existing))
        {
            seen[key] = info;
            return;
        }
        // Prefer entries that resolved an exe path.
        if (string.IsNullOrEmpty(existing.ExecutablePath) && !string.IsNullOrEmpty(info.ExecutablePath))
            seen[key] = info;
    }

    // ===== Uninstall registry =====

    private IEnumerable<InstalledAppInfo> ReadUninstallEntries(RegistryKey hive, RegistryView view)
    {
        using var baseKey = RegistryKey.OpenBaseKey(hive.Name == "HKEY_LOCAL_MACHINE" ? RegistryHive.LocalMachine : RegistryHive.CurrentUser, view);
        foreach (var root in UninstallRoots)
        {
            using var rootKey = baseKey.OpenSubKey(root);
            if (rootKey is null) continue;
            foreach (var sub in rootKey.GetSubKeyNames())
            {
                InstalledAppInfo? info;
                try
                {
                    using var k = rootKey.OpenSubKey(sub);
                    if (k is null) continue;

                    var displayName = (k.GetValue("DisplayName") as string)?.Trim();
                    if (string.IsNullOrEmpty(displayName)) continue;
                    if (IsSystemEntry(k, displayName)) continue;
                    if (IsBoring(displayName)) continue;

                    var publisher = (k.GetValue("Publisher") as string)?.Trim();
                    var version = (k.GetValue("DisplayVersion") as string)?.Trim();
                    var installLocation = (k.GetValue("InstallLocation") as string)?.Trim();
                    var displayIcon = (k.GetValue("DisplayIcon") as string)?.Trim();

                    var exePath = ResolveExeFromUninstall(displayIcon, installLocation, displayName);
                    info = new InstalledAppInfo
                    {
                        DisplayName = displayName,
                        Publisher = publisher,
                        Version = version,
                        ExecutablePath = exePath,
                        ProcessName = string.IsNullOrEmpty(exePath) ? null : Path.GetFileNameWithoutExtension(exePath),
                        Source = "Registry",
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Failed to read uninstall key {Sub}.", sub);
                    continue;
                }
                yield return info;
            }
        }
    }

    private static bool IsSystemEntry(RegistryKey k, string displayName)
    {
        if (k.GetValue("SystemComponent") is int sc && sc != 0) return true;
        if (k.GetValue("ParentKeyName") is string) return true;
        return false;
    }

    private static bool IsBoring(string displayName) =>
        BoringPrefixes.Any(p => displayName.StartsWith(p, StringComparison.OrdinalIgnoreCase));

    private static string? ResolveExeFromUninstall(string? displayIcon, string? installLocation, string? displayName)
    {
        // DisplayIcon often points to the main exe directly: "C:\Program Files\Discord\Discord.exe,0"
        if (!string.IsNullOrEmpty(displayIcon))
        {
            var path = displayIcon;
            var comma = path.IndexOf(',', StringComparison.Ordinal);
            if (comma >= 0) path = path.Substring(0, comma);
            path = path.Trim().Trim('"');
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                return path;
        }
        // InstallLocation: pick the most "named like the app" exe in the folder.
        if (!string.IsNullOrEmpty(installLocation) && Directory.Exists(installLocation))
        {
            try
            {
                var exes = Directory.GetFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly);
                if (exes.Length == 0) return null;
                if (exes.Length == 1) return exes[0];
                if (!string.IsNullOrEmpty(displayName))
                {
                    var hint = displayName.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();
                    var match = exes.FirstOrDefault(e => Path.GetFileNameWithoutExtension(e).Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant().Contains(hint, StringComparison.Ordinal));
                    if (!string.IsNullOrEmpty(match)) return match;
                }
                return exes.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).First();
            }
            catch (UnauthorizedAccessException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (IOException) { return null; }
        }
        return null;
    }

    // ===== App Paths registry =====

    private IEnumerable<InstalledAppInfo> ReadAppPaths(RegistryKey hive, RegistryView view)
    {
        using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
        using var rootKey = baseKey.OpenSubKey(AppPathsRoot);
        if (rootKey is null) yield break;
        foreach (var sub in rootKey.GetSubKeyNames())
        {
            InstalledAppInfo? info;
            try
            {
                using var k = rootKey.OpenSubKey(sub);
                if (k is null) continue;
                var path = (k.GetValue(null) as string)?.Trim().Trim('"');
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                if (!File.Exists(path)) continue;
                var displayName = TryReadFileDescription(path)
                                 ?? Path.GetFileNameWithoutExtension(sub);
                if (IsBoring(displayName)) continue;
                info = new InstalledAppInfo
                {
                    DisplayName = displayName,
                    ExecutablePath = path,
                    ProcessName = Path.GetFileNameWithoutExtension(path),
                    Source = "AppPaths",
                };
            }
            catch (Exception ex)
            {
                _logger.LogTrace(ex, "Failed to read App Paths key {Sub}.", sub);
                continue;
            }
            yield return info;
        }
    }

    // ===== Start Menu .lnk shortcuts =====

    private IEnumerable<InstalledAppInfo> ReadStartMenuShortcuts()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        };
        foreach (var root in roots)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
            string[] lnks;
            try
            {
                lnks = Directory.GetFiles(root, "*.lnk", SearchOption.AllDirectories);
            }
            catch (UnauthorizedAccessException) { continue; }
            catch (DirectoryNotFoundException) { continue; }
            catch (IOException) { continue; }

            foreach (var lnk in lnks)
            {
                InstalledAppInfo? info = null;
                try
                {
                    var target = ShortcutResolver.ResolveTarget(lnk);
                    if (string.IsNullOrEmpty(target) || !target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!File.Exists(target)) continue;
                    var displayName = TryReadFileDescription(target)
                                     ?? Path.GetFileNameWithoutExtension(lnk);
                    if (IsBoring(displayName)) continue;
                    info = new InstalledAppInfo
                    {
                        DisplayName = displayName,
                        ExecutablePath = target,
                        ProcessName = Path.GetFileNameWithoutExtension(target),
                        Source = "StartMenu",
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Failed to resolve shortcut {Lnk}.", lnk);
                }
                if (info is not null) yield return info;
            }
        }
    }

    private static string? TryReadFileDescription(string exePath)
    {
        try
        {
            var info = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            var fd = info.FileDescription?.Trim();
            return string.IsNullOrEmpty(fd) ? null : fd;
        }
        catch (Exception) { return null; }
    }
}
