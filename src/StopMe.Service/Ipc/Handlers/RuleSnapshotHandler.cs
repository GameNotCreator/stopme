using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.State;

namespace StopMe.Service.Ipc.Handlers;

public sealed class RuleSnapshotHandler : IOperationHandler
{
    private readonly ServiceState _state;

    public RuleSnapshotHandler(ServiceState state) => _state = state;

    public string Op => Operations.RuleSnapshot;

    public Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var snapshot = _state.Current;
        var dto = new RuleSnapshotResponse
        {
            ComputedAtUtc = snapshot.ComputedAtUtc,
            Rules = snapshot.CompiledRules.Select(r => new CompiledRuleDto
            {
                RuleId = r.RuleId,
                BlocklistId = r.BlocklistId,
                Type = r.Type.ToString(),
                Value = r.Value,
                SourceMode = r.SourceMode.ToString(),
                SessionId = r.SessionId,
                SessionName = r.SessionName,
                SessionEndUtc = r.SessionEndUtc,
            }).ToArray(),
        };
        return Task.FromResult(LocalApiResponse.Ok(dto));
    }
}
