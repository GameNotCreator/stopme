namespace StopMe.Core.Enums;

public enum StopReason
{
    UserRequested = 0,
    Expired = 1,
    SystemShutdown = 2,
    Error = 3,
    ReplacedByAnotherSession = 4,
}
