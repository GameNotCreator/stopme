using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using StopMe.Core.Abstractions;

namespace StopMe.Service.Blocking;

// Implements the spec's IInternetBlockingProvider with a real, deployable
// mechanism: rewrite the Windows hosts file to redirect blocked domains to
// 127.0.0.1, then let LocalBlockedPageServer serve the "blocked" page.
//
// The contract in IInternetBlockingProvider only carries "AllowedDomains",
// which is the inverse of what we need. The real per-tick blocked-domain list
// lives in the compiled RuleSnapshot held by ServiceState — we pull it from
// there. This keeps the abstraction stable without complicating it.
[SupportedOSPlatform("windows")]
public sealed class HostsFileInternetBlockingProvider : IInternetBlockingProvider
{
    private readonly HostsFile _hosts;
    private readonly ILogger<HostsFileInternetBlockingProvider> _logger;
    private HashSet<string> _lastApplied = new(StringComparer.OrdinalIgnoreCase);

    // When the service runs without admin, every write fails with
    // UnauthorizedAccessException. The provider used to retry each tick (2s)
    // and HostsFile.cs logged LogError every time — a flood of identical
    // errors. We flip this flag on the first denial, log a single warning,
    // and treat the provider as a no-op until the process restarts. The
    // browser extension still enforces blocks at the network layer.
    private bool _writeDeniedOnce;

    public HostsFileInternetBlockingProvider(HostsFile hosts, ILogger<HostsFileInternetBlockingProvider> logger)
    {
        _hosts = hosts;
        _logger = logger;
    }

    public string Capability => "HostsFile";

    public Task ApplyAsync(InternetBlockingState state, CancellationToken ct = default)
    {
        if (!state.Active)
        {
            return DeactivateAsync(ct);
        }
        return Task.CompletedTask;
    }

    public Task ApplyDomainsAsync(IReadOnlyCollection<string> domains, CancellationToken ct = default)
    {
        if (_writeDeniedOnce) return Task.CompletedTask;

        var desired = new HashSet<string>(
            domains.Select(HostsFile.NormalizeDomain).Where(d => !string.IsNullOrEmpty(d)),
            StringComparer.OrdinalIgnoreCase);

        if (desired.SetEquals(_lastApplied))
            return Task.CompletedTask;

        if (desired.Count == 0)
        {
            return DeactivateAsync(ct);
        }

        var (ok, denied) = _hosts.ApplyBlocklist(desired);
        if (denied)
        {
            _writeDeniedOnce = true;
            _logger.LogWarning(
                "Hosts-file blocking is disabled: write to {Path} denied (admin required). " +
                "Web blocking will rely on the browser extension only until the service is restarted with admin rights.",
                HostsFile.FilePath);
            return Task.CompletedTask;
        }
        if (ok)
        {
            _hosts.FlushDns();
            _lastApplied = desired;
            _logger.LogInformation("Hosts file updated with {Count} blocked domain(s).", desired.Count);
        }
        return Task.CompletedTask;
    }

    public Task DeactivateAsync(CancellationToken ct = default)
    {
        if (_writeDeniedOnce) return Task.CompletedTask;
        if (_lastApplied.Count == 0 && !HostsFile.HasStopMeBlock())
            return Task.CompletedTask;
        var (ok, denied) = _hosts.Restore();
        if (denied)
        {
            _writeDeniedOnce = true;
            _logger.LogWarning("Hosts-file restore denied (admin required). Leaving file untouched.");
            return Task.CompletedTask;
        }
        if (ok)
        {
            _hosts.FlushDns();
            _lastApplied = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _logger.LogInformation("Hosts file restored — no active website blocks.");
        }
        return Task.CompletedTask;
    }
}
