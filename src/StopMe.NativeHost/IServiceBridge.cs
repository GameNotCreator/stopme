using StopMe.LocalApi.Dtos;

namespace StopMe.NativeHost;

public interface IServiceBridge : IAsyncDisposable
{
    Task<bool> EnsureConnectedAsync(CancellationToken ct);

    Task<StatusDto?> GetStatusAsync(CancellationToken ct);

    Task<IReadOnlyList<ActiveSessionDto>> GetActiveSessionsAsync(CancellationToken ct);

    Task<EvaluateUrlResponse?> EvaluateUrlAsync(string url, string? browser, CancellationToken ct);

    Task<bool> SendHeartbeatAsync(ExtensionHeartbeatRequest request, CancellationToken ct);

    string? LastError { get; }
}
