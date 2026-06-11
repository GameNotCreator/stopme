namespace StopMe.Core.Abstractions;

// Tiny key/value store backed by the Settings SQLite table. All consumers should
// namespace keys ("stats.privacy_mode", "service.last_seen_version", …).
public interface ISettingsRepository
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);

    Task SetAsync(string key, string value, CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default);
}
