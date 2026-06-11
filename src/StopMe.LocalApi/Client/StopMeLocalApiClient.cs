using System.IO.Pipes;
using StopMe.LocalApi.Dtos;
using StopMe.LocalApi.Protocol;

namespace StopMe.LocalApi.Client;

public sealed class StopMeLocalApiClient : IAsyncDisposable
{
    private readonly NamedPipeClientStream _pipe;
    private readonly string? _token;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public StopMeLocalApiClient(string? token = null, string serverName = ".", string? pipeNameOverride = null)
    {
        _token = token;
        _pipe = new NamedPipeClientStream(
            serverName: serverName,
            pipeName: string.IsNullOrWhiteSpace(pipeNameOverride) ? LocalApiProtocol.PipeName : pipeNameOverride!,
            direction: PipeDirection.InOut,
            options: PipeOptions.Asynchronous);
    }

    public bool IsConnected => _pipe.IsConnected;

    public async Task ConnectAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        await _pipe.ConnectAsync((int)timeout.TotalMilliseconds, ct).ConfigureAwait(false);
        _pipe.ReadMode = PipeTransmissionMode.Byte;
    }

    public Task<LocalApiResponse?> StatusAsync(CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.Status, _token), ct);

    public Task<LocalApiResponse?> ActiveSessionsAsync(CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.ActiveSessions, _token), ct);

    public Task<LocalApiResponse?> StartSessionAsync(Guid sessionId, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.StartSession, new StartSessionRequest { SessionId = sessionId }, _token), ct);

    public Task<LocalApiResponse?> StopSessionAsync(StopSessionRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.StopSession, request, _token), ct);

    public Task<LocalApiResponse?> ListBlocklistsAsync(CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.ListBlocklists, _token), ct);

    public Task<LocalApiResponse?> CreateBlocklistAsync(CreateBlocklistRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.CreateBlocklist, request, _token), ct);

    public Task<LocalApiResponse?> DeleteBlocklistAsync(Guid id, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.DeleteBlocklist, new DeleteBlocklistRequest { Id = id }, _token), ct);

    public Task<LocalApiResponse?> EvaluateUrlAsync(EvaluateUrlRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.EvaluateUrl, request, _token), ct);

    public Task<LocalApiResponse?> EvaluateAppAsync(EvaluateAppRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.EvaluateApp, request, _token), ct);

    public Task<LocalApiResponse?> RuleSnapshotAsync(CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.RuleSnapshot, _token), ct);

    public Task<LocalApiResponse?> ExtensionHeartbeatAsync(ExtensionHeartbeatRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.ExtensionHeartbeat, request, _token), ct);

    public Task<LocalApiResponse?> ExtensionHealthAsync(CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.ExtensionHealth, _token), ct);

    public Task<LocalApiResponse?> BypassEventsAsync(CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.BypassEvents, _token), ct);

    public Task<LocalApiResponse?> StatsSummaryAsync(StatsQueryRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.StatsSummary, request, _token), ct);

    public Task<LocalApiResponse?> StatsExportCsvAsync(StatsExportRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.StatsExportCsv, request, _token), ct);

    public Task<LocalApiResponse?> StatsResetAsync(CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.StatsReset, _token), ct);

    public Task<LocalApiResponse?> SettingsGetAsync(SettingsGetRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.SettingsGet, request, _token), ct);

    public Task<LocalApiResponse?> SettingsSetAsync(SettingsSetRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.SettingsSet, request, _token), ct);

    public Task<LocalApiResponse?> TemplatesListAsync(CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.TemplatesList, _token), ct);

    public Task<LocalApiResponse?> TemplatesApplyAsync(TemplateApplyRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.TemplatesApply, request, _token), ct);

    public Task<LocalApiResponse?> ProcessSnapshotAsync(CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.ProcessSnapshot, _token), ct);

    public Task<LocalApiResponse?> CreateSessionAsync(CreateSessionRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.CreateSession, request, _token), ct);

    public Task<LocalApiResponse?> AddRuleAsync(AddRuleRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.AddRule, request, _token), ct);

    public Task<LocalApiResponse?> InstalledAppsAsync(CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.InstalledApps, _token), ct);

    public Task<LocalApiResponse?> SearchDomainsAsync(DomainSearchRequest request, CancellationToken ct = default) =>
        SendAsync(LocalApiRequest.For(Operations.SearchDomains, request, _token), ct);

    public async Task<LocalApiResponse?> SendAsync(LocalApiRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_pipe.IsConnected)
            throw new InvalidOperationException("Client is not connected.");

        await _sendLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await PipeFraming.WriteAsync(_pipe, request, ct).ConfigureAwait(false);
            return await PipeFraming.ReadAsync<LocalApiResponse>(_pipe, ct).ConfigureAwait(false);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _sendLock.Dispose();
        await _pipe.DisposeAsync().ConfigureAwait(false);
    }
}
