using StopMe.Core.Abstractions;
using StopMe.Service.State;

namespace StopMe.Service.Antibypass;

// Bridges the Service-layer ServiceState.ClockDriftActive flag onto the
// Core-layer IClockDriftMonitor contract so SessionEngine can deny strict
// stops while drift is active.
public sealed class ServiceStateClockDriftMonitor : IClockDriftMonitor
{
    private readonly ServiceState _state;
    public ServiceStateClockDriftMonitor(ServiceState state) => _state = state;
    public bool IsClockDriftActive => _state.ClockDriftActive;
}
