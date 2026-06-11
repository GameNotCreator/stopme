using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Search;

public interface IDomainSearchProvider
{
    string Name { get; }

    Task<IReadOnlyList<DomainSearchHitDto>> SearchAsync(string query, int maxResults, CancellationToken ct = default);
}
