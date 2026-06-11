using Microsoft.EntityFrameworkCore;
using StopMe.Core.Abstractions;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;

namespace StopMe.Infrastructure.Repositories;

public sealed class SettingsRepository : ISettingsRepository
{
    private readonly StopMeDbContext _db;
    private readonly TimeProvider _clock;

    public SettingsRepository(StopMeDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var row = await _db.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, ct)
            .ConfigureAwait(false);
        return row?.Value;
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        var now = _clock.GetUtcNow().UtcDateTime;
        var existing = await _db.Settings.FindAsync(new object?[] { key }, ct).ConfigureAwait(false);
        if (existing is null)
        {
            _db.Settings.Add(new Setting { Key = key, Value = value, UpdatedAtUtc = now });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAtUtc = now;
        }
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default)
    {
        var rows = await _db.Settings
            .AsNoTracking()
            .ToListAsync(ct)
            .ConfigureAwait(false);
        return rows.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);
    }
}
