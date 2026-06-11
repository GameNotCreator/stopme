using StopMe.App.Core.Services;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.Tests.Fakes;

internal sealed class FakeServiceConnection : IServiceConnection
{
    public ConnectionState State { get; set; } = ConnectionState.Connected;

    public string? LastError { get; set; }

    public event EventHandler? StateChanged;

    public StatusDto? StatusToReturn { get; set; }
    public List<ActiveSessionDto> ActiveSessionsToReturn { get; } = new();
    public List<BlocklistDto> BlocklistsToReturn { get; } = new();
    public EvaluateUrlResponse? EvaluateUrlResponse { get; set; }
    public StopSessionResponse? StopSessionResponseToReturn { get; set; }
    public bool StartSessionResult { get; set; } = true;
    public bool DeleteBlocklistResult { get; set; } = true;

    public List<CreateBlocklistRequest> Created { get; } = new();
    public List<Guid> Deleted { get; } = new();
    public List<StopSessionRequest> StopRequests { get; } = new();
    public List<Guid> StartedSessions { get; } = new();

    public void RaiseStateChanged(ConnectionState newState)
    {
        State = newState;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<bool> EnsureConnectedAsync(CancellationToken ct = default) =>
        Task.FromResult(State == ConnectionState.Connected);

    public Task<StatusDto?> GetStatusAsync(CancellationToken ct = default) =>
        Task.FromResult<StatusDto?>(StatusToReturn);

    public Task<IReadOnlyList<ActiveSessionDto>> GetActiveSessionsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ActiveSessionDto>>(ActiveSessionsToReturn.ToArray());

    public List<SessionListItemDto> SessionListToReturn { get; } = new();
    public List<string?> SessionListStatusFilters { get; } = new();

    public Task<IReadOnlyList<SessionListItemDto>> GetSessionListAsync(string? statusFilter = null, CancellationToken ct = default)
    {
        SessionListStatusFilters.Add(statusFilter);
        return Task.FromResult<IReadOnlyList<SessionListItemDto>>(SessionListToReturn.ToArray());
    }

    public Task<IReadOnlyList<BlocklistDto>> ListBlocklistsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BlocklistDto>>(BlocklistsToReturn.ToArray());

    public Task<BlocklistDto?> CreateBlocklistAsync(CreateBlocklistRequest request, CancellationToken ct = default)
    {
        Created.Add(request);
        var created = new BlocklistDto
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Mode = request.Mode,
            IsEnabled = true,
            Rules = (request.Rules ?? Array.Empty<CreateRuleRequest>())
                .Select(r => new BlockRuleDto
                {
                    Id = Guid.NewGuid(),
                    Type = r.Type,
                    Value = r.Value,
                    IsEnabled = true,
                }).ToArray(),
        };
        BlocklistsToReturn.Add(created);
        return Task.FromResult<BlocklistDto?>(created);
    }

    public Task<bool> DeleteBlocklistAsync(Guid id, CancellationToken ct = default)
    {
        Deleted.Add(id);
        BlocklistsToReturn.RemoveAll(b => b.Id == id);
        return Task.FromResult(DeleteBlocklistResult);
    }

    public Task<EvaluateUrlResponse?> EvaluateUrlAsync(string url, CancellationToken ct = default) =>
        Task.FromResult(EvaluateUrlResponse);

    public Task<bool> StartSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        StartedSessions.Add(sessionId);
        return Task.FromResult(StartSessionResult);
    }

    public Task<StopSessionResponse?> StopSessionAsync(StopSessionRequest request, CancellationToken ct = default)
    {
        StopRequests.Add(request);
        return Task.FromResult<StopSessionResponse?>(StopSessionResponseToReturn);
    }

    public StatsSummaryResponse? StatsResponseToReturn { get; set; }
    public StatsExportResponse? StatsExportResponseToReturn { get; set; }
    public int StatsResetCount { get; set; }
    public Dictionary<string, string> SettingsToReturn { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<KeyValuePair<string, string>> SettingsSets { get; } = new();
    public bool SetSettingResult { get; set; } = true;
    public List<BypassEventDto> BypassEventsToReturn { get; } = new();
    public string? LastStatsPeriodRequested { get; set; }
    public string? LastStatsExportPeriodRequested { get; set; }
    public int StatsResetInvocations { get; private set; }

    public Task<StatsSummaryResponse?> GetStatsAsync(string period, CancellationToken ct = default)
    {
        LastStatsPeriodRequested = period;
        return Task.FromResult<StatsSummaryResponse?>(StatsResponseToReturn);
    }

    public Task<StatsExportResponse?> ExportStatsCsvAsync(string period, CancellationToken ct = default)
    {
        LastStatsExportPeriodRequested = period;
        return Task.FromResult<StatsExportResponse?>(StatsExportResponseToReturn);
    }

    public Task<int> ResetStatsAsync(CancellationToken ct = default)
    {
        StatsResetInvocations++;
        return Task.FromResult(StatsResetCount);
    }

    public Task<IReadOnlyDictionary<string, string>> GetSettingsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(SettingsToReturn, StringComparer.OrdinalIgnoreCase));

    public Task<bool> SetSettingAsync(string key, string value, CancellationToken ct = default)
    {
        SettingsSets.Add(new KeyValuePair<string, string>(key, value));
        SettingsToReturn[key] = value;
        return Task.FromResult(SetSettingResult);
    }

    public Task<IReadOnlyList<BypassEventDto>> GetBypassEventsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BypassEventDto>>(BypassEventsToReturn.ToArray());

    public List<TemplateDto> TemplatesToReturn { get; } = new();
    public TemplateApplyResponse? ApplyTemplateResponseToReturn { get; set; }
    public List<(string Key, DateTime StartAtUtc)> ApplyTemplateCalls { get; } = new();

    public Task<IReadOnlyList<TemplateDto>> ListTemplatesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TemplateDto>>(TemplatesToReturn.ToArray());

    public Task<TemplateApplyResponse?> ApplyTemplateAsync(string templateKey, DateTime startAtUtc, CancellationToken ct = default)
    {
        ApplyTemplateCalls.Add((templateKey, startAtUtc));
        return Task.FromResult(ApplyTemplateResponseToReturn);
    }

    public List<RunningAppInfoDto> RunningAppsToReturn { get; } = new();
    public CreateSessionResponse? CreateSessionResponseToReturn { get; set; }
    public List<CreateSessionRequest> CreateSessionRequests { get; } = new();
    public AddRuleResponse? AddRulesResponseToReturn { get; set; }
    public List<AddRuleRequest> AddRulesRequests { get; } = new();

    public Task<IReadOnlyList<RunningAppInfoDto>> GetRunningAppsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<RunningAppInfoDto>>(RunningAppsToReturn.ToArray());

    public Task<CreateSessionResponse?> CreateSessionAsync(CreateSessionRequest request, CancellationToken ct = default)
    {
        CreateSessionRequests.Add(request);
        return Task.FromResult(CreateSessionResponseToReturn);
    }

    public Task<AddRuleResponse?> AddRulesAsync(AddRuleRequest request, CancellationToken ct = default)
    {
        AddRulesRequests.Add(request);
        return Task.FromResult(AddRulesResponseToReturn);
    }

    public List<InstalledAppInfoDto> InstalledAppsToReturn { get; } = new();
    public List<DomainSearchHitDto> DomainSearchHitsToReturn { get; } = new();
    public List<string> DomainSearchQueries { get; } = new();

    public Task<IReadOnlyList<InstalledAppInfoDto>> GetInstalledAppsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<InstalledAppInfoDto>>(InstalledAppsToReturn.ToArray());

    public Task<IReadOnlyList<DomainSearchHitDto>> SearchDomainsAsync(string query, int maxResults = 12, CancellationToken ct = default)
    {
        DomainSearchQueries.Add(query);
        return Task.FromResult<IReadOnlyList<DomainSearchHitDto>>(DomainSearchHitsToReturn.Take(maxResults).ToArray());
    }
}
