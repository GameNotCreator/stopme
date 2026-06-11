using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.State;

namespace StopMe.Service.Ipc.Handlers;

public sealed class UpdateBlocklistHandler : IOperationHandler
{
    private readonly IBlocklistRepository _repo;
    private readonly IStrictModeGuard _guard;
    private readonly ServiceState _state;
    private readonly IBypassEventRepository _bypass;
    private readonly TimeProvider _clock;

    public UpdateBlocklistHandler(
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

    public string Op => Operations.UpdateBlocklist;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<UpdateBlocklistRequest>();
        if (dto is null || dto.Id == Guid.Empty)
            return LocalApiResponse.Error(ErrorCodes.BadRequest, "Missing or invalid id.");

        var decision = _guard.CanMutateBlocklist(dto.Id, _state.Current.ActiveSessions);
        if (!decision.IsAllowed)
        {
            await _bypass.AddAsync(new BypassEvent
            {
                Type = BypassEventType.BlocklistEditAttempted,
                DetectedAtUtc = _clock.GetUtcNow().UtcDateTime,
                SessionId = decision.BlockingSessionId,
                Source = nameof(UpdateBlocklistHandler),
                Detail = decision.DenialReason,
            }, ct).ConfigureAwait(false);
            return LocalApiResponse.Error(ErrorCodes.Conflict, decision.DenialReason!);
        }

        var existing = await _repo.GetByIdAsync(dto.Id, ct).ConfigureAwait(false);
        if (existing is null) return LocalApiResponse.Error(ErrorCodes.NotFound, "Blocklist not found.");

        if (!string.IsNullOrWhiteSpace(dto.Name)) existing.Name = dto.Name.Trim();
        if (dto.Description is not null) existing.Description = dto.Description.Trim();
        if (!string.IsNullOrWhiteSpace(dto.Mode))
        {
            if (!Mapping.TryParseBlocklistMode(dto.Mode, out var mode))
                return LocalApiResponse.Error(ErrorCodes.BadRequest, $"Invalid mode '{dto.Mode}'.");
            existing.Mode = mode;
        }
        if (dto.IsEnabled.HasValue) existing.IsEnabled = dto.IsEnabled.Value;

        existing.UpdatedAtUtc = _clock.GetUtcNow().UtcDateTime;
        await _repo.UpdateAsync(existing, ct).ConfigureAwait(false);

        return LocalApiResponse.Ok(Mapping.ToDto(existing));
    }
}
