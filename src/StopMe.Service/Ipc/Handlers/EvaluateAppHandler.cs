using StopMe.Core.Abstractions;
using StopMe.Core.ValueObjects;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.State;

namespace StopMe.Service.Ipc.Handlers;

public sealed class EvaluateAppHandler : IOperationHandler
{
    private readonly IAppRuleEngine _engine;
    private readonly ServiceState _state;

    public EvaluateAppHandler(IAppRuleEngine engine, ServiceState state)
    {
        _engine = engine;
        _state = state;
    }

    public string Op => Operations.EvaluateApp;

    public Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<EvaluateAppRequest>();
        if (dto is null || dto.ProcessId <= 0 || string.IsNullOrWhiteSpace(dto.ProcessName))
            return Task.FromResult(LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing process info."));

        var snapshot = _state.Current;
        var app = new RunningAppInfo
        {
            ProcessId = dto.ProcessId,
            ProcessName = dto.ProcessName,
            ExecutablePath = dto.ExecutablePath,
            MainWindowTitle = dto.MainWindowTitle,
            PackageFamilyName = dto.PackageFamilyName,
            IsMicrosoftStoreApp = dto.IsMicrosoftStoreApp,
        };
        var decision = _engine.Evaluate(app, snapshot.CompiledRules);
        return Task.FromResult(LocalApiResponse.Ok(Mapping.ToResponse(decision)));
    }
}
