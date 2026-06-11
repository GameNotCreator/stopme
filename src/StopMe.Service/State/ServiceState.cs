namespace StopMe.Service.State;

// Shared, lock-free snapshot of the most recent worker tick. Writes happen on the worker
// thread (single writer); reads happen from any number of IPC handlers.
public sealed class ServiceState
{
    private RuleSnapshot _current = RuleSnapshot.Empty;
    private long _lastTickUtcTicks;
    private int _bootRestoreCompleted;

    public RuleSnapshot Current => Volatile.Read(ref _current);

    public DateTime LastTickUtc => new(Interlocked.Read(ref _lastTickUtcTicks), DateTimeKind.Utc);

    public bool BootRestoreCompleted => Volatile.Read(ref _bootRestoreCompleted) != 0;

    // Set by AntiBypassRunner whenever clock drift is observed during a strict
    // session. SessionEngine.CanStopSessionAsync consults this flag and denies
    // early stops while it is set — turning "drift detected" from a passive log
    // into an active anti-bypass control. Cleared when a clean tick passes.
    private int _clockDriftActive;
    public bool ClockDriftActive => Volatile.Read(ref _clockDriftActive) != 0;

    public void UpdateSnapshot(RuleSnapshot snapshot, DateTime tickUtc)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Volatile.Write(ref _current, snapshot);
        Interlocked.Exchange(ref _lastTickUtcTicks, tickUtc.Ticks);
    }

    public void MarkBootRestoreCompleted() => Interlocked.Exchange(ref _bootRestoreCompleted, 1);

    public void SetClockDriftActive(bool active) =>
        Interlocked.Exchange(ref _clockDriftActive, active ? 1 : 0);
}
