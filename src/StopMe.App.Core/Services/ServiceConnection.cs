using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StopMe.LocalApi;
using StopMe.LocalApi.Client;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.Services;

public sealed class ServiceConnection : IServiceConnection, IAsyncDisposable
{
    private readonly IOptions<ServiceConnectionOptions> _options;
    private readonly ILogger<ServiceConnection> _logger;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private StopMeLocalApiClient? _client;
    private ConnectionState _state = ConnectionState.Disconnected;
    private string? _lastError;

    public ServiceConnection(IOptions<ServiceConnectionOptions> options, ILogger<ServiceConnection> logger)
    {
        _options = options;
        _logger = logger;
    }

    public ConnectionState State => _state;

    public string? LastError => _lastError;

    public event EventHandler? StateChanged;

    public async Task<bool> EnsureConnectedAsync(CancellationToken ct = default)
    {
        if (_state == ConnectionState.Connected && _client is { IsConnected: true })
            return true;

        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_state == ConnectionState.Connected && _client is { IsConnected: true })
                return true;

            await DisposeClientAsync().ConfigureAwait(false);

            SetState(ConnectionState.Connecting);

            var opts = _options.Value;
            var dataDir = opts.ResolveDataDirectory();
            string? token;
            try
            {
                // Important: the UI must NEVER create the token. The service owns it.
                // If the file is missing it almost always means the service has never
                // run; show a clear actionable message instead of silently writing a
                // fake token that the service would later reject.
                var store = new TokenStore(dataDir);
                token = store.TryLoad();
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                Fail($"Cannot read auth.token from {dataDir}: {ex.Message}. " +
                     "Run StopMe.Service from an elevated prompt or check the data-directory ACL.");
                return false;
            }

            if (string.IsNullOrEmpty(token))
            {
                Fail($"StopMe service is not running. Start the service (or run deploy\\install.ps1), then click Retry. " +
                     $"Expected auth.token at {dataDir}.");
                return false;
            }

            _client = new StopMeLocalApiClient(token, pipeNameOverride: opts.PipeNameOverride);
            try
            {
                await _client.ConnectAsync(opts.ConnectTimeout, ct).ConfigureAwait(false);
                SetState(ConnectionState.Connected);
                _logger.LogInformation("Connected to StopMe service via local pipe.");
                return true;
            }
            catch (TimeoutException)
            {
                Fail("Could not connect to the StopMe service within "
                    + $"{(int)opts.ConnectTimeout.TotalSeconds}s. Is StopMe.Service running?");
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                Fail("Service refused the auth token. The token may have been rotated — restart the service.");
                return false;
            }
            catch (Exception ex)
            {
                Fail($"Connection failed: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task<StatusDto?> GetStatusAsync(CancellationToken ct = default) =>
        await SendAsync<StatusDto>(c => c.StatusAsync(ct), ct).ConfigureAwait(false);

    public async Task<IReadOnlyList<ActiveSessionDto>> GetActiveSessionsAsync(CancellationToken ct = default) =>
        await SendAsync<ActiveSessionDto[]>(c => c.ActiveSessionsAsync(ct), ct).ConfigureAwait(false) ?? Array.Empty<ActiveSessionDto>();

    public async Task<IReadOnlyList<BlocklistDto>> ListBlocklistsAsync(CancellationToken ct = default) =>
        await SendAsync<BlocklistDto[]>(c => c.ListBlocklistsAsync(ct), ct).ConfigureAwait(false) ?? Array.Empty<BlocklistDto>();

    public Task<BlocklistDto?> CreateBlocklistAsync(CreateBlocklistRequest request, CancellationToken ct = default) =>
        SendAsync<BlocklistDto>(c => c.CreateBlocklistAsync(request, ct), ct);

    public async Task<bool> DeleteBlocklistAsync(Guid id, CancellationToken ct = default)
    {
        var resp = await SendRawAsync(c => c.DeleteBlocklistAsync(id, ct), ct).ConfigureAwait(false);
        return resp?.Success ?? false;
    }

    public Task<EvaluateUrlResponse?> EvaluateUrlAsync(string url, CancellationToken ct = default) =>
        SendAsync<EvaluateUrlResponse>(c => c.EvaluateUrlAsync(new EvaluateUrlRequest { Url = url }, ct), ct);

    public async Task<bool> StartSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var resp = await SendRawAsync(c => c.StartSessionAsync(sessionId, ct), ct).ConfigureAwait(false);
        return resp?.Success ?? false;
    }

    public Task<StopSessionResponse?> StopSessionAsync(StopSessionRequest request, CancellationToken ct = default) =>
        SendAsync<StopSessionResponse>(c => c.StopSessionAsync(request, ct), ct);

    public Task<StatsSummaryResponse?> GetStatsAsync(string period, CancellationToken ct = default) =>
        SendAsync<StatsSummaryResponse>(c => c.StatsSummaryAsync(new StatsQueryRequest { Period = period }, ct), ct);

    public Task<StatsExportResponse?> ExportStatsCsvAsync(string period, CancellationToken ct = default) =>
        SendAsync<StatsExportResponse>(c => c.StatsExportCsvAsync(new StatsExportRequest { Period = period }, ct), ct);

    public async Task<int> ResetStatsAsync(CancellationToken ct = default)
    {
        var resp = await SendAsync<StatsResetResponse>(c => c.StatsResetAsync(ct), ct).ConfigureAwait(false);
        return resp?.DeletedEvents ?? 0;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetSettingsAsync(CancellationToken ct = default)
    {
        var resp = await SendAsync<SettingsGetResponse>(c => c.SettingsGetAsync(new SettingsGetRequest(), ct), ct).ConfigureAwait(false);
        return resp?.Entries ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> SetSettingAsync(string key, string value, CancellationToken ct = default)
    {
        var resp = await SendRawAsync(c => c.SettingsSetAsync(new SettingsSetRequest { Key = key, Value = value }, ct), ct).ConfigureAwait(false);
        return resp?.Success ?? false;
    }

    public async Task<IReadOnlyList<BypassEventDto>> GetBypassEventsAsync(CancellationToken ct = default)
    {
        var resp = await SendAsync<BypassEventsResponse>(c => c.BypassEventsAsync(ct), ct).ConfigureAwait(false);
        return resp?.Events ?? Array.Empty<BypassEventDto>();
    }

    public async Task<IReadOnlyList<TemplateDto>> ListTemplatesAsync(CancellationToken ct = default)
    {
        var resp = await SendAsync<TemplatesListResponse>(c => c.TemplatesListAsync(ct), ct).ConfigureAwait(false);
        return resp?.Templates ?? Array.Empty<TemplateDto>();
    }

    public Task<TemplateApplyResponse?> ApplyTemplateAsync(string templateKey, DateTime startAtUtc, CancellationToken ct = default) =>
        SendAsync<TemplateApplyResponse>(c => c.TemplatesApplyAsync(new TemplateApplyRequest
        {
            TemplateKey = templateKey,
            StartAtUtc = startAtUtc,
        }, ct), ct);

    public async Task<IReadOnlyList<RunningAppInfoDto>> GetRunningAppsAsync(CancellationToken ct = default)
    {
        var resp = await SendAsync<ProcessSnapshotResponse>(c => c.ProcessSnapshotAsync(ct), ct).ConfigureAwait(false);
        return resp?.Apps ?? Array.Empty<RunningAppInfoDto>();
    }

    public Task<CreateSessionResponse?> CreateSessionAsync(CreateSessionRequest request, CancellationToken ct = default) =>
        SendAsync<CreateSessionResponse>(c => c.CreateSessionAsync(request, ct), ct);

    public Task<AddRuleResponse?> AddRulesAsync(AddRuleRequest request, CancellationToken ct = default) =>
        SendAsync<AddRuleResponse>(c => c.AddRuleAsync(request, ct), ct);

    public async Task<IReadOnlyList<InstalledAppInfoDto>> GetInstalledAppsAsync(CancellationToken ct = default)
    {
        var resp = await SendAsync<InstalledAppsResponse>(c => c.InstalledAppsAsync(ct), ct).ConfigureAwait(false);
        return resp?.Apps ?? Array.Empty<InstalledAppInfoDto>();
    }

    public async Task<IReadOnlyList<DomainSearchHitDto>> SearchDomainsAsync(string query, int maxResults = 12, CancellationToken ct = default)
    {
        var resp = await SendAsync<DomainSearchResponse>(c => c.SearchDomainsAsync(new DomainSearchRequest
        {
            Query = query,
            MaxResults = maxResults,
        }, ct), ct).ConfigureAwait(false);
        return resp?.Hits ?? Array.Empty<DomainSearchHitDto>();
    }

    private async Task<T?> SendAsync<T>(Func<StopMeLocalApiClient, Task<LocalApiResponse?>> call, CancellationToken ct) where T : class
    {
        var resp = await SendRawAsync(call, ct).ConfigureAwait(false);
        if (resp is null || !resp.Success) return null;
        return resp.PayloadAs<T>();
    }

    private async Task<LocalApiResponse?> SendRawAsync(Func<StopMeLocalApiClient, Task<LocalApiResponse?>> call, CancellationToken ct)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false)) return null;
        try
        {
            return await call(_client!).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            Fail($"Pipe error: {ex.Message}");
            return null;
        }
        catch (InvalidOperationException ex)
        {
            Fail($"Protocol error: {ex.Message}");
            return null;
        }
    }

    private void Fail(string message)
    {
        _lastError = message;
        _logger.LogWarning("ServiceConnection failed: {Reason}", message);
        SetState(ConnectionState.Failed);
    }

    private void SetState(ConnectionState state)
    {
        if (_state == state) return;
        _state = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task DisposeClientAsync()
    {
        if (_client is not null)
        {
            try { await _client.DisposeAsync().ConfigureAwait(false); }
            catch { /* best-effort */ }
            _client = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeClientAsync().ConfigureAwait(false);
        _connectLock.Dispose();
    }
}
