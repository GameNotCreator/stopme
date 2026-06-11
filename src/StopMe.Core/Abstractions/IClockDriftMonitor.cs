namespace StopMe.Core.Abstractions;

// Tiny abstraction so SessionEngine (Core) can ask the Service-layer
// AntiBypassRunner / ServiceState whether clock drift is currently active.
// Default registration in Core is "always false" so unit tests / pure Core
// hosts keep behaving as before.
public interface IClockDriftMonitor
{
    bool IsClockDriftActive { get; }
}

public sealed class NullClockDriftMonitor : IClockDriftMonitor
{
    public bool IsClockDriftActive => false;
}
