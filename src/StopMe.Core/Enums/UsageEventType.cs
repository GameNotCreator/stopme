namespace StopMe.Core.Enums;

public enum UsageEventType
{
    WebsiteBlocked = 0,
    AppBlocked = 1,
    SessionStarted = 2,
    SessionCompleted = 3,
    SessionCancelled = 4,
    PauseStarted = 5,
    PauseEnded = 6,
    BypassAttemptDetected = 7,
    ExtensionDisabledDetected = 8,
}
