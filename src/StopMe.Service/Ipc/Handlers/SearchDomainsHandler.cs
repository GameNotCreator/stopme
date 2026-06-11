using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.Search;

namespace StopMe.Service.Ipc.Handlers;

// Keyword → domain lookup. Sits service-side so the UI never embeds API keys
// (and so the call goes through the bounded HttpClientFactory pool instead of
// the WinUI thread).
public sealed class SearchDomainsHandler : IOperationHandler
{
    private readonly IDomainSearchProvider _provider;

    public SearchDomainsHandler(IDomainSearchProvider provider) => _provider = provider;

    public string Op => Operations.SearchDomains;

    public async Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var dto = request.PayloadAs<DomainSearchRequest>();
        if (dto is null || string.IsNullOrWhiteSpace(dto.Query))
        {
            return LocalApiResponse.Error(ErrorCodes.BadRequest, "Query is required.");
        }
        var hits = await _provider.SearchAsync(dto.Query, dto.MaxResults, ct).ConfigureAwait(false);
        return LocalApiResponse.Ok(new DomainSearchResponse
        {
            Hits = hits,
            Provider = _provider.Name,
        });
    }
}
