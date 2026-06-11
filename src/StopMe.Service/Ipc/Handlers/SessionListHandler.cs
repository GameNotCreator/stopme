using StopMe.Core.Abstractions;
using StopMe.Core.Enums;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Ipc.Handlers;

// Surfaces the persisted FocusSession set so the WinUI Sessions page can
// render Scheduled / Recurring / Past buckets instead of the prototype demo
// rows. Replaces SessionsViewModel's hardcoded TODO.
public sealed class SessionListHandler : IOperationHandler
{
    private readonly ISessionRepository _repo;
    private readonly IUserContext _user;

    public SessionListHandler(ISessionRepository repo, IUserContext user)
    {
        _repo = repo;
        _user = user;
    }

    public string Op => Operations.SessionList;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<SessionListRequest>();

        var sessions = await _repo.GetAllAsync(_user.CurrentUserProfileId, ct).ConfigureAwait(false);

        var filter = dto?.StatusFilter;
        IEnumerable<Core.Models.FocusSession> filtered = sessions;
        if (!string.IsNullOrWhiteSpace(filter)
            && Enum.TryParse<SessionStatus>(filter, ignoreCase: true, out var status))
        {
            filtered = filtered.Where(s => s.Status == status);
        }

        var items = filtered
            .OrderBy(s => s.StartAtUtc)
            .Select(s => new SessionListItemDto
            {
                SessionId = s.Id,
                Name = s.Name,
                StartAtUtc = s.StartAtUtc,
                EndAtUtc = s.EndAtUtc,
                Status = s.Status.ToString(),
                Mode = s.Mode.ToString(),
                LockPolicy = s.LockPolicy.ToString(),
                IsRecurring = s.IsRecurring,
                RecurrenceRule = s.RecurrenceRule,
                TimeZoneId = s.TimeZoneId,
                BlocklistIds = s.SessionBlocklists?.Select(sb => sb.BlocklistId).ToArray()
                    ?? Array.Empty<Guid>(),
            })
            .ToArray();

        return LocalApiResponse.Ok(items);
    }
}
