using Microsoft.EntityFrameworkCore;
using StopMe.Core.Abstractions;
using StopMe.Infrastructure.Persistence;

namespace StopMe.Infrastructure.Repositories;

public sealed class CategoryRepository : ICategoryRepository
{
    private readonly StopMeDbContext _db;

    public CategoryRepository(StopMeDbContext db) => _db = db;

    public async Task<IReadOnlyList<string>> GetDomainsAsync(string categoryName, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(categoryName);
        return await _db.CategoryDomains
            .AsNoTracking()
            .Where(c => c.CategoryName == categoryName)
            .Select(c => c.Domain)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> GetCategoryNamesAsync(CancellationToken ct = default) =>
        await _db.CategoryDomains
            .AsNoTracking()
            .Select(c => c.CategoryName)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct)
            .ConfigureAwait(false);
}
