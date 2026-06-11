using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.State;

namespace StopMe.Service.Ipc.Handlers;

public sealed class DeleteBlocklistHandler : IOperationHandler
{
    private readonly IBlocklistRepository _repo;
    private readonly IStrictModeGuard _guard;
    private readonly ServiceState _state;
    private readonly IBypassEventRepository _bypass;
    private readonly TimeProvider _clock;

    public DeleteBlocklistHandler(
        IBlocklistRepository repo,
        IStrictModeGuard guard,
        ServiceState state,
        IBypassEventRepository bypass,
        TimeProvider clock)
    {
        _repo = repo;
        _guard = guard;
        _state = state;
        _bypass = bypass;
        _clock = clock;
    }

    public string Op => Operations.DeleteBlocklist;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<DeleteBlocklistRequest>();
        if (dto is null || dto.Id == Guid.Empty)
            return LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing or invalid id.");

        // Previously: the handler called DeleteAsync without first checking
        // existence, so a request with an unknown ID returned Ok(). That hid
        // bugs in the client (e.g. a stale ID being silently swallowed).
        var existing = await _repo.GetByIdAsync(dto.Id, ct).ConfigureAwait(false);
        if (existing is null)
            return LocalApiResponse.Error(ErrorCodes.NotFound, $"Blocklist {dto.Id} not found.");

        var decision = _guard.CanDeleteBlocklist(dto.Id, _state.Current.ActiveSessions);
        if (!decision.IsAllowed)
        {
            await _bypass.AddAsync(new BypassEvent
            {
                Type = BypassEventType.BlocklistDeleteAttempted,
                DetectedAtUtc = _clock.GetUtcNow().UtcDateTime,
                SessionId = decision.BlockingSessionId,
                Source = nameof(DeleteBlocklistHandler),
                Detail = decision.DenialReason,
            }, ct).ConfigureAwait(false);
            return LocalApiResponse.Error(ErrorCodes.Conflict, decision.DenialReason!);
        }

        await _repo.DeleteAsync(dto.Id, ct).ConfigureAwait(false);
        return LocalApiResponse.Ok();
    }
}
