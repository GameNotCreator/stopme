namespace StopMe.Service.Processes;

// Snapshot of one installed application discovered via the Windows registry
// (Uninstall keys) and Start Menu .lnk shortcuts.
public sealed class InstalledAppInfo
{
    public required string DisplayName { get; init; }

    public string? Publisher { get; init; }

    public string? Version { get; init; }

    // Full path to the main exe (best-effort: resolved from InstallLocation +
    // shortcut targets + Uninstall DisplayIcon). Null when we couldn't resolve one.
    public string? ExecutablePath { get; init; }

    // The process name we'd match against at session time, e.g. "discord".
    // Derived from the resolved exe; null when no exe could be resolved.
    public string? ProcessName { get; init; }

    // Source of the entry — primarily for diagnostics.
    public required string Source { get; init; } // "Registry" | "StartMenu" | "AppPaths"
}
