using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;

namespace StopMe.Infrastructure.Seed;

public sealed class CategorySeeder
{
    private const string ResourceName = "StopMe.Infrastructure.Seed.default-categories.json";
    private const string Source = "builtin";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly StopMeDbContext _db;

    public CategorySeeder(StopMeDbContext db) => _db = db;

    public async Task<int> SeedAsync(CancellationToken ct = default)
    {
        var payload = LoadEmbedded();
        if (payload?.Categories is null) return 0;

        var existing = await _db.CategoryDomains
            .AsNoTracking()
            .Where(c => c.Source == Source)
            .Select(c => new { c.CategoryName, c.Domain })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var existingSet = new HashSet<(string, string)>(
            existing.Select(e => (e.CategoryName, e.Domain)),
            new TupleComparer());

        var now = DateTime.UtcNow;
        var added = 0;

        foreach (var category in payload.Categories)
        {
            if (string.IsNullOrWhiteSpace(category.Name) || category.Domains is null) continue;
            foreach (var domain in category.Domains)
            {
                if (string.IsNullOrWhiteSpace(domain)) continue;
                var pair = (category.Name, domain);
                if (existingSet.Contains(pair)) continue;
                _db.CategoryDomains.Add(new CategoryDomain
                {
                    Id = Guid.NewGuid(),
                    CategoryName = category.Name,
                    Domain = domain,
                    Source = Source,
                    CreatedAtUtc = now,
                });
                existingSet.Add(pair);
                added++;
            }
        }

        if (added > 0) await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return added;
    }

    private static SeedPayload? LoadEmbedded()
    {
        using var stream = typeof(CategorySeeder).Assembly
            .GetManifestResourceStream(ResourceName);
        if (stream is null)
            throw new InvalidOperationException($"Embedded resource '{ResourceName}' not found. " +
                $"Found: {string.Join(", ", typeof(CategorySeeder).Assembly.GetManifestResourceNames())}");
        return JsonSerializer.Deserialize<SeedPayload>(stream, JsonOptions);
    }

    private sealed class SeedPayload
    {
        public List<SeedCategory> Categories { get; set; } = new();
    }

    private sealed class SeedCategory
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Domains { get; set; } = new();
    }

    private sealed class TupleComparer : IEqualityComparer<(string CategoryName, string Domain)>
    {
        public bool Equals((string CategoryName, string Domain) x, (string CategoryName, string Domain) y) =>
            string.Equals(x.CategoryName, y.CategoryName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Domain, y.Domain, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string CategoryName, string Domain) obj) =>
            HashCode.Combine(
                obj.CategoryName.ToUpperInvariant(),
                obj.Domain.ToUpperInvariant());
    }

}
