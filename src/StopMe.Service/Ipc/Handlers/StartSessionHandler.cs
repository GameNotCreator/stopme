using StopMe.Core.Abstractions;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Ipc.Handlers;

public sealed class StartSessionHandler : IOperationHandler
{
    private readonly ISessionEngine _engine;
    private readonly TimeProvider _clock;

    public StartSessionHandler(ISessionEngine engine, TimeProvider clock)
    {
        _engine = engine;
        _clock = clock;
    }

    public string Op => Operations.StartSession;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<StartSessionRequest>();
        if (dto is null || dto.SessionId == Guid.Empty)
            return LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing or invalid sessionId.");

        try
        {
            await _engine.StartSessionAsync(dto.SessionId, _clock.GetUtcNow().UtcDateTime, ct).ConfigureAwait(false);
            return LocalApiResponse.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return LocalApiResponse.Error(ErrorCodes.Conflict, ex.Message);
        }
    }
}
