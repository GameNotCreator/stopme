namespace StopMe.Core.Enums;

public enum BypassEventType
{
    ExtensionStale = 0,
    ExtensionDisabled = 1,
    UnsupportedBrowser = 2,
    ClockDrift = 3,
    ServiceStopAttempt = 4,
    CriticalFileTampered = 5,
    SessionEditAttempted = 6,
    BlocklistEditAttempted = 7,
    BlocklistDeleteAttempted = 8,
    Other = 99,
}
