using StopMe.Service.Processes;

namespace StopMe.Service.Tests.Fakes;

internal sealed class FakeProcessTerminator : IProcessTerminator
{
    public List<(int Pid, string Name)> Terminations { get; } = new();

    public TerminationResult NextResult { get; set; } = TerminationResult.GracefullyClosed;

    public Func<int, string, TerminationResult>? Configure { get; set; }

    public Task<TerminationResult> CloseOrKillAsync(int processId, string processName, CancellationToken ct)
    {
        Terminations.Add((processId, processName));
        var result = Configure?.Invoke(processId, processName) ?? NextResult;
        return Task.FromResult(result);
    }
}
