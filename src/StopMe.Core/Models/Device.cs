namespace StopMe.Core.Models;

public class Device
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserProfileId { get; set; }

    public string DeviceName { get; set; } = string.Empty;

    public string Platform { get; set; } = "windows";

    public string MachineIdHash { get; set; } = string.Empty;

    public DateTime RegisteredAtUtc { get; set; }

    public DateTime LastSeenAtUtc { get; set; }

    public bool SyncEnabled { get; set; }
}
