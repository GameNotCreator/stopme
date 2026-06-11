namespace StopMe.App.Core.Models;

public enum AppTheme
{
    System = 0,
    Light = 1,
    Dark = 2,
}

public sealed class AppSettingsState
{
    public AppTheme Theme { get; set; } = AppTheme.System;
    public bool StartAtBoot { get; set; }
    public bool ShowTrayIcon { get; set; } = true;
    public string? DataDirectoryOverride { get; set; }
}
