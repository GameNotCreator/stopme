using StopMe.LocalApi;
using StopMe.Service.State;

namespace StopMe.Service.Ipc.Handlers;

public sealed class ActiveSessionsHandler : IOperationHandler
{
    private readonly ServiceState _state;
    private readonly TimeProvider _clock;

    public ActiveSessionsHandler(ServiceState state, TimeProvider clock)
    {
        _state = state;
        _clock = clock;
    }

    public string Op => Operations.ActiveSessions;

    public Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var snapshot = _state.Current;
        var dtos = snapshot.ActiveSessions.Select(s => Mapping.ToDto(s, nowUtc)).ToArray();
        return Task.FromResult(LocalApiResponse.Ok(dtos));
    }
}
