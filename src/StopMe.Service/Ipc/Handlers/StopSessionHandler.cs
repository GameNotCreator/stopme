using StopMe.Core.Abstractions;
using StopMe.Core.ValueObjects;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Ipc.Handlers;

public sealed class StopSessionHandler : IOperationHandler
{
    private readonly ISessionEngine _engine;
    private readonly TimeProvider _clock;

    public StopSessionHandler(ISessionEngine engine, TimeProvider clock)
    {
        _engine = engine;
        _clock = clock;
    }

    public string Op => Operations.StopSession;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<StopSessionRequest>();
        if (dto is null || dto.SessionId == Guid.Empty)
            return LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing or invalid sessionId.");

        if (!Mapping.TryParseStopReason(dto.Reason, out var reason))
            return LocalApiResponse.Error(ErrorCodes.BadRequest, $"Invalid stop reason '{dto.Reason}'.");

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var stopRequest = new StopRequest
        {
            Reason = reason,
            UserConfirmed = dto.UserConfirmed,
            Password = dto.Password,
            TypedText = dto.TypedText,
        };

        var attempt = await _engine.CanStopSessionAsync(dto.SessionId, stopRequest, nowUtc, ct).ConfigureAwait(false);
        if (!attempt.IsAllowed)
        {
            return LocalApiResponse.Ok(new StopSessionResponse
            {
                Stopped = false,
                DenialReason = attempt.DenialReason,
            });
        }

        await _engine.StopSessionAsync(dto.SessionId, reason, nowUtc, ct).ConfigureAwait(false);
        return LocalApiResponse.Ok(new StopSessionResponse { Stopped = true });
    }
}
