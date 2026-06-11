namespace StopMe.Service.Antibypass;

public sealed class AntiBypassOptions
{
    public int ExtensionStaleAfterSeconds { get; set; } = 180;

    public int ClockDriftThresholdSeconds { get; set; } = 300;

    // Hysteresis: once drift is detected, keep the flag set for this many
    // seconds of clean samples before clearing. Prevents a forward-jump
    // bypass attempt from being "self-healed" within one tick when the wall
    // clock stabilises immediately after the jump.
    public int ClockDriftCooldownSeconds { get; set; } = 120;
}
