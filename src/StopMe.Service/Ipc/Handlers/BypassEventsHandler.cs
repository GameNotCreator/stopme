using StopMe.Core.Abstractions;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Ipc.Handlers;

public sealed class BypassEventsHandler : IOperationHandler
{
    private const int DefaultLimit = 100;

    private readonly IBypassEventRepository _repo;

    public BypassEventsHandler(IBypassEventRepository repo) => _repo = repo;

    public string Op => Operations.BypassEvents;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var events = await _repo.GetRecentAsync(DefaultLimit, ct).ConfigureAwait(false);
        var dto = new BypassEventsResponse
        {
            Events = events.Select(e => new BypassEventDto
            {
                Id = e.Id,
                Type = e.Type.ToString(),
                DetectedAtUtc = e.DetectedAtUtc,
                SessionId = e.SessionId,
                Source = e.Source,
                Detail = e.Detail,
            }).ToArray(),
        };
        return LocalApiResponse.Ok(dto);
    }
}
