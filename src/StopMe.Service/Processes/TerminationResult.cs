namespace StopMe.Service.Processes;

public enum TerminationResult
{
    GracefullyClosed = 0,
    Killed = 1,
    AlreadyGone = 2,
    AccessDenied = 3,
    Failed = 4,
}
