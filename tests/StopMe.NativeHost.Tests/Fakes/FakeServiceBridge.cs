using StopMe.LocalApi.Dtos;
using StopMe.NativeHost;

namespace StopMe.NativeHost.Tests.Fakes;

internal sealed class FakeServiceBridge : IServiceBridge
{
    public bool Connected { get; set; } = true;
    public string? LastError { get; set; }

    public StatusDto? StatusToReturn { get; set; }
    public List<ActiveSessionDto> ActiveSessionsToReturn { get; } = new();
    public EvaluateUrlResponse? UrlResponseToReturn { get; set; }
    public bool HeartbeatResult { get; set; } = true;

    public List<string> EvaluatedUrls { get; } = new();
    public List<ExtensionHeartbeatRequest> Heartbeats { get; } = new();

    public Task<bool> EnsureConnectedAsync(CancellationToken ct) => Task.FromResult(Connected);

    public Task<StatusDto?> GetStatusAsync(CancellationToken ct) => Task.FromResult(StatusToReturn);

    public Task<IReadOnlyList<ActiveSessionDto>> GetActiveSessionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ActiveSessionDto>>(ActiveSessionsToReturn.ToArray());

    public Task<EvaluateUrlResponse?> EvaluateUrlAsync(string url, string? browser, CancellationToken ct)
    {
        EvaluatedUrls.Add(url);
        return Task.FromResult<EvaluateUrlResponse?>(UrlResponseToReturn);
    }

    public Task<bool> SendHeartbeatAsync(ExtensionHeartbeatRequest request, CancellationToken ct)
    {
        Heartbeats.Add(request);
        return Task.FromResult(HeartbeatResult);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
