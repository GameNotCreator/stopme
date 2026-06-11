using StopMe.Core.Abstractions;
using StopMe.LocalApi;

namespace StopMe.Service.Ipc.Handlers;

public sealed class ListBlocklistsHandler : IOperationHandler
{
    private readonly IBlocklistRepository _repo;
    private readonly IUserContext _user;

    public ListBlocklistsHandler(IBlocklistRepository repo, IUserContext user)
    {
        _repo = repo;
        _user = user;
    }

    public string Op => Operations.ListBlocklists;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var lists = await _repo.GetAllAsync(_user.CurrentUserProfileId, ct).ConfigureAwait(false);
        var dtos = lists.Select(Mapping.ToDto).ToArray();
        return LocalApiResponse.Ok(dtos);
    }
}
