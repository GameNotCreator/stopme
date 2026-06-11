namespace StopMe.Service.Processes;

public interface IInstalledAppsProvider
{
    // Enumerate all user-facing applications installed on the machine.
    // The result is de-duplicated by DisplayName + ExecutablePath.
    IReadOnlyList<InstalledAppInfo> EnumerateInstalled();
}
