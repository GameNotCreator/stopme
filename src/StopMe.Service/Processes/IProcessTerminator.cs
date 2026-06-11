namespace StopMe.Service.Processes;

public interface IProcessTerminator
{
    Task<TerminationResult> CloseOrKillAsync(int processId, string processName, CancellationToken ct);
}
