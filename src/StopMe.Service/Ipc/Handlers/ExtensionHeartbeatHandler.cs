using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.Antibypass;

namespace StopMe.Service.Ipc.Handlers;

public sealed class ExtensionHeartbeatHandler : IOperationHandler
{
    private readonly IExtensionHealthMonitor _health;
    private readonly TimeProvider _clock;

    public ExtensionHeartbeatHandler(IExtensionHealthMonitor health, TimeProvider clock)
    {
        _health = health;
        _clock = clock;
    }

    public string Op => Operations.ExtensionHeartbeat;

    public Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<ExtensionHeartbeatRequest>();
        if (dto is null || string.IsNullOrWhiteSpace(dto.Browser) || string.IsNullOrWhiteSpace(dto.ExtensionId))
            return Task.FromResult(LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing browser/extensionId."));

        _health.RecordHeartbeat(new ExtensionHeartbeat
        {
            Browser = dto.Browser.Trim(),
            ExtensionId = dto.ExtensionId.Trim(),
            ExtensionVersion = dto.ExtensionVersion,
        }, _clock.GetUtcNow().UtcDateTime);

        return Task.FromResult(LocalApiResponse.Ok());
    }
}
