namespace StopMe.Core.Abstractions;

// Phase 8 abstraction over the §12 "Full Internet" enforcement layer.
//   - BrowserOnly       : Phase 4 / 6 reality — only Chromium tabs are intercepted.
//   - WindowsFirewall   : future — `netsh advfirewall` rules per process.
//   - WindowsFiltering  : future — full WFP integration (no kernel driver).
//
// SessionEngine doesn't talk to providers directly today; the contract is
// captured so that BlocklistMode.FullInternet can hook in later without
// changing the rule pipeline.
public interface IInternetBlockingProvider
{
    string Capability { get; }

    Task ApplyAsync(InternetBlockingState state, CancellationToken ct = default);
}

public sealed class InternetBlockingState
{
    public required bool Active { get; init; }

    // Domains the user explicitly allow-listed in a FullInternet blocklist.
    public required IReadOnlyList<string> AllowedDomains { get; init; }
}
