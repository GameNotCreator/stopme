namespace StopMe.Core.Abstractions;

public interface ICategoryRepository
{
    Task<IReadOnlyList<string>> GetDomainsAsync(string categoryName, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetCategoryNamesAsync(CancellationToken ct = default);
}
