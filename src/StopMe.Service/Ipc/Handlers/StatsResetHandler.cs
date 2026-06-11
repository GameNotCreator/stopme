using StopMe.Core.Abstractions;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Ipc.Handlers;

// Wipes all UsageEvent rows. Triggered from the privacy panel when the user
// wants a clean slate. Blocklists, sessions, settings are NOT touched.
public sealed class StatsResetHandler : IOperationHandler
{
    private readonly IUsageEventRepository _events;

    public StatsResetHandler(IUsageEventRepository events) => _events = events;

    public string Op => Operations.StatsReset;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var deleted = await _events.DeleteAllAsync(ct).ConfigureAwait(false);
        return LocalApiResponse.Ok(new StatsResetResponse { DeletedEvents = deleted });
    }
}
