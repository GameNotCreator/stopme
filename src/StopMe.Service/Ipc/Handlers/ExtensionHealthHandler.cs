using Microsoft.Extensions.Options;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.Antibypass;

namespace StopMe.Service.Ipc.Handlers;

public sealed class ExtensionHealthHandler : IOperationHandler
{
    private readonly IExtensionHealthMonitor _health;
    private readonly IOptionsMonitor<AntiBypassOptions> _options;
    private readonly TimeProvider _clock;

    public ExtensionHealthHandler(IExtensionHealthMonitor health, IOptionsMonitor<AntiBypassOptions> options, TimeProvider clock)
    {
        _health = health;
        _options = options;
        _clock = clock;
    }

    public string Op => Operations.ExtensionHealth;

    public Task<LocalApiResponse> HandleAsync(LocalApiRequest request, CancellationToken ct)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var stale = TimeSpan.FromSeconds(Math.Max(1, _options.CurrentValue.ExtensionStaleAfterSeconds));
        var snapshots = _health.SnapshotAll();

        var dto = new ExtensionHealthDto
        {
            StaleThresholdSeconds = (int)stale.TotalSeconds,
            Browsers = snapshots.Select(s =>
            {
                var since = nowUtc - s.LastHeartbeatUtc;
                return new ExtensionBrowserHealthDto
                {
                    Browser = s.Browser,
                    ExtensionId = s.ExtensionId,
                    ExtensionVersion = s.ExtensionVersion,
                    LastHeartbeatUtc = s.LastHeartbeatUtc,
                    SecondsSinceLastHeartbeat = since < TimeSpan.Zero ? 0 : (int)since.TotalSeconds,
                    IsStale = since > stale,
                };
            }).ToArray(),
        };

        return Task.FromResult(LocalApiResponse.Ok(dto));
    }
}
