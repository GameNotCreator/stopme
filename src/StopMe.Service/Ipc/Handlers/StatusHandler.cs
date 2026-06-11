using System.Reflection;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.State;

namespace StopMe.Service.Ipc.Handlers;

public sealed class StatusHandler : IOperationHandler
{
    private static readonly string Version =
        typeof(StatusHandler).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    private readonly ServiceState _state;
    private readonly TimeProvider _clock;

    public StatusHandler(ServiceState state, TimeProvider clock)
    {
        _state = state;
        _clock = clock;
    }

    public string Op => Operations.Status;

    public Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var snapshot = _state.Current;
        var dto = new StatusDto
        {
            ServiceVersion = Version,
            NowUtc = _clock.GetUtcNow().UtcDateTime,
            LastTickUtc = _state.LastTickUtc,
            BootRestoreCompleted = _state.BootRestoreCompleted,
            ActiveSessionCount = snapshot.ActiveSessions.Count,
            CompiledRuleCount = snapshot.CompiledRules.Count,
        };
        return Task.FromResult(LocalApiResponse.Ok(dto));
    }
}
