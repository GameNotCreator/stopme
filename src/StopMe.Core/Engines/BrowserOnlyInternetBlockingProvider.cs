using StopMe.Core.Abstractions;

namespace StopMe.Core.Engines;

// The default Phase 8 provider: full-Internet blocking is delegated to the
// browser extension's existing CHECK_URL pipeline. ApplyAsync is a no-op — the
// rule snapshot already carries any FullInternet rule the UrlRuleEngine needs
// to block traffic at the extension layer.
//
// A Firewall/WFP provider would override this in a future package.
public sealed class BrowserOnlyInternetBlockingProvider : IInternetBlockingProvider
{
    public string Capability => "BrowserOnly";

    public Task ApplyAsync(InternetBlockingState state, CancellationToken ct = default) =>
        Task.CompletedTask;
}
