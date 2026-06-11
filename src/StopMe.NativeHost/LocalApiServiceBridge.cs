using Microsoft.Extensions.Logging;
using StopMe.LocalApi;
using StopMe.LocalApi.Client;
using StopMe.LocalApi.Dtos;

namespace StopMe.NativeHost;

public sealed class LocalApiServiceBridge : IServiceBridge
{
    private readonly NativeHostOptions _options;
    private readonly ILogger<LocalApiServiceBridge> _logger;
    private readonly SemaphoreSlim _connectLock = new(1, 1);

    private StopMeLocalApiClient? _client;
    private string? _lastError;

    public LocalApiServiceBridge(NativeHostOptions options, ILogger<LocalApiServiceBridge> logger)
    {
        _options = options;
        _logger = logger;
    }

    public string? LastError => _lastError;

    public async Task<bool> EnsureConnectedAsync(CancellationToken ct)
    {
        if (_client is { IsConnected: true }) return true;

        await _connectLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_client is { IsConnected: true }) return true;

            await DisposeClientAsync().ConfigureAwait(false);

            string token;
            try
            {
                var store = new TokenStore(_options.DataDirectory);
                var loaded = store.TryLoad();
                if (string.IsNullOrEmpty(loaded))
                {
                    Fail($"auth.token missing under {_options.DataDirectory}; the StopMe service may not have started yet.");
                    return false;
                }
                token = loaded;
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                Fail($"Cannot read auth.token: {ex.Message}");
                return false;
            }

            _client = new StopMeLocalApiClient(token);
            try
            {
                await _client.ConnectAsync(_options.ConnectTimeout, ct).ConfigureAwait(false);
                _lastError = null;
                return true;
            }
            catch (TimeoutException)
            {
                Fail("Connection to StopMe service timed out — is the service running?");
                return false;
            }
            catch (Exception ex)
            {
                Fail($"Connection failed: {ex.Message}");
                return false;
            }
        }
        finally
        {
            _connectLock.Release();
        }
    }

    public async Task<StatusDto?> GetStatusAsync(CancellationToken ct)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false)) return null;
        try
        {
            var resp = await _client!.StatusAsync(ct).ConfigureAwait(false);
            if (resp is null || !resp.Success) { Fail(resp?.ErrorMessage ?? "no response"); return null; }
            return resp.PayloadAs<StatusDto>();
        }
        catch (IOException ex) { Fail(ex.Message); return null; }
    }

    public async Task<IReadOnlyList<ActiveSessionDto>> GetActiveSessionsAsync(CancellationToken ct)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false)) return Array.Empty<ActiveSessionDto>();
        try
        {
            var resp = await _client!.ActiveSessionsAsync(ct).ConfigureAwait(false);
            if (resp is null || !resp.Success) { Fail(resp?.ErrorMessage ?? "no response"); return Array.Empty<ActiveSessionDto>(); }
            return resp.PayloadAs<ActiveSessionDto[]>() ?? Array.Empty<ActiveSessionDto>();
        }
        catch (IOException ex) { Fail(ex.Message); return Array.Empty<ActiveSessionDto>(); }
    }

    public async Task<EvaluateUrlResponse?> EvaluateUrlAsync(string url, string? browser, CancellationToken ct)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false)) return null;
        try
        {
            var resp = await _client!.EvaluateUrlAsync(new EvaluateUrlRequest { Url = url, Browser = browser }, ct).ConfigureAwait(false);
            if (resp is null || !resp.Success) { Fail(resp?.ErrorMessage ?? "no response"); return null; }
            return resp.PayloadAs<EvaluateUrlResponse>();
        }
        catch (IOException ex) { Fail(ex.Message); return null; }
    }

    public async Task<bool> SendHeartbeatAsync(ExtensionHeartbeatRequest request, CancellationToken ct)
    {
        if (!await EnsureConnectedAsync(ct).ConfigureAwait(false)) return false;
        try
        {
            var resp = await _client!.ExtensionHeartbeatAsync(request, ct).ConfigureAwait(false);
            if (resp is null || !resp.Success) { Fail(resp?.ErrorMessage ?? "no response"); return false; }
            return true;
        }
        catch (IOException ex) { Fail(ex.Message); return false; }
    }

    private void Fail(string message)
    {
        _lastError = message;
        _logger.LogWarning("Service bridge: {Reason}", message);
    }

    private async Task DisposeClientAsync()
    {
        if (_client is null) return;
        try { await _client.DisposeAsync().ConfigureAwait(false); }
        catch { /* best-effort */ }
        _client = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeClientAsync().ConfigureAwait(false);
        _connectLock.Dispose();
    }
}
