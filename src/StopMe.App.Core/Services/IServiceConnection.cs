using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.Services;

public interface IServiceConnection
{
    ConnectionState State { get; }

    string? LastError { get; }

    event EventHandler? StateChanged;

    Task<bool> EnsureConnectedAsync(CancellationToken ct = default);

    Task<StatusDto?> GetStatusAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ActiveSessionDto>> GetActiveSessionsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<BlocklistDto>> ListBlocklistsAsync(CancellationToken ct = default);

    Task<BlocklistDto?> CreateBlocklistAsync(CreateBlocklistRequest request, CancellationToken ct = default);

    Task<bool> DeleteBlocklistAsync(Guid id, CancellationToken ct = default);

    Task<EvaluateUrlResponse?> EvaluateUrlAsync(string url, CancellationToken ct = default);

    Task<bool> StartSessionAsync(Guid sessionId, CancellationToken ct = default);

    Task<StopSessionResponse?> StopSessionAsync(StopSessionRequest request, CancellationToken ct = default);

    Task<StatsSummaryResponse?> GetStatsAsync(string period, CancellationToken ct = default);

    Task<StatsExportResponse?> ExportStatsCsvAsync(string period, CancellationToken ct = default);

    Task<int> ResetStatsAsync(CancellationToken ct = default);

    Task<IReadOnlyDictionary<string, string>> GetSettingsAsync(CancellationToken ct = default);

    Task<bool> SetSettingAsync(string key, string value, CancellationToken ct = default);

    Task<IReadOnlyList<BypassEventDto>> GetBypassEventsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<TemplateDto>> ListTemplatesAsync(CancellationToken ct = default);

    Task<TemplateApplyResponse?> ApplyTemplateAsync(string templateKey, DateTime startAtUtc, CancellationToken ct = default);

    Task<IReadOnlyList<RunningAppInfoDto>> GetRunningAppsAsync(CancellationToken ct = default);

    Task<CreateSessionResponse?> CreateSessionAsync(CreateSessionRequest request, CancellationToken ct = default);

    Task<AddRuleResponse?> AddRulesAsync(AddRuleRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<InstalledAppInfoDto>> GetInstalledAppsAsync(CancellationToken ct = default);

    Task<IReadOnlyList<DomainSearchHitDto>> SearchDomainsAsync(string query, int maxResults = 12, CancellationToken ct = default);
}
