using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.Processes;

namespace StopMe.Service.Ipc.Handlers;

// Returns a coarse snapshot of the running apps the service can see, so the UI
// can offer a "pick what to block" picker without re-implementing process
// enumeration. Hidden / system processes already filtered by the provider.
public sealed class ProcessSnapshotHandler : IOperationHandler
{
    private readonly IProcessProvider _provider;
    private readonly TimeProvider _clock;

    public ProcessSnapshotHandler(IProcessProvider provider, TimeProvider clock)
    {
        _provider = provider;
        _clock = clock;
    }

    public string Op => Operations.ProcessSnapshot;

    public Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        // Group by process name so the user picks a "block discord.exe" rule once,
        // not one per Discord helper process.
        var apps = _provider.SnapshotRunning()
            .GroupBy(a => a.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var first = g.First();
                var withWindow = g.FirstOrDefault(x => !string.IsNullOrEmpty(x.MainWindowTitle)) ?? first;
                return new RunningAppInfoDto
                {
                    ProcessId = withWindow.ProcessId,
                    ProcessName = first.ProcessName,
                    ExecutablePath = g.Select(x => x.ExecutablePath).FirstOrDefault(p => !string.IsNullOrEmpty(p)),
                    MainWindowTitle = withWindow.MainWindowTitle,
                    HasVisibleWindow = !string.IsNullOrEmpty(withWindow.MainWindowTitle),
                };
            })
            .OrderByDescending(a => a.HasVisibleWindow)
            .ThenBy(a => a.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult(LocalApiResponse.Ok(new ProcessSnapshotResponse
        {
            Apps = apps,
            CapturedAtUtc = _clock.GetUtcNow().UtcDateTime,
        }));
    }
}
