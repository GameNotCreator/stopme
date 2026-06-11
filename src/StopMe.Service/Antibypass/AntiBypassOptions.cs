namespace StopMe.Service.Antibypass;

public sealed class AntiBypassOptions
{
    public int ExtensionStaleAfterSeconds { get; set; } = 180;

    public int ClockDriftThresholdSeconds { get; set; } = 300;
}
