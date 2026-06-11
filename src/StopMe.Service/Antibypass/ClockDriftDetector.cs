namespace StopMe.Service.Antibypass;

public sealed record ClockDriftSample(TimeSpan WallDelta, TimeSpan MonotonicDelta, TimeSpan Drift);

// Compares wall-clock progression against a monotonic counter. When the user (or another
// process) jumps the clock backward or forward to escape a strict session, the wall-clock
// delta deviates from the monotonic one by more than the configured threshold and we emit
// a sample (the caller turns it into a BypassEvent).
public sealed class ClockDriftDetector
{
    private readonly Func<long> _monotonicMs;
    private DateTime? _lastWallUtc;
    private long _lastMonotonicMs;

    public ClockDriftDetector(Func<long>? monotonicMs = null)
    {
        _monotonicMs = monotonicMs ?? (() => Environment.TickCount64);
    }

    public ClockDriftSample? Sample(DateTime nowUtc, TimeSpan threshold)
    {
        var monoMs = _monotonicMs();
        ClockDriftSample? drift = null;
        if (_lastWallUtc.HasValue)
        {
            var wallDelta = nowUtc - _lastWallUtc.Value;
            var monoDelta = TimeSpan.FromMilliseconds(monoMs - _lastMonotonicMs);
            var diff = wallDelta - monoDelta;
            if (diff.Duration() > threshold)
                drift = new ClockDriftSample(wallDelta, monoDelta, diff);
        }
        _lastWallUtc = nowUtc;
        _lastMonotonicMs = monoMs;
        return drift;
    }

    public void Reset()
    {
        _lastWallUtc = null;
        _lastMonotonicMs = 0;
    }
}
