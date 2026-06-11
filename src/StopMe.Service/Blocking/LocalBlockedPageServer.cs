using System.Net;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StopMe.Service.State;

namespace StopMe.Service.Blocking;

// Tiny HTTP listener bound to 127.0.0.1:80. When a blocked domain has been
// redirected to loopback via the hosts file, the browser's request lands here
// and we serve the "This one can wait." page from the design handoff.
//
// Notes / caveats:
//   * Binds the wildcard URL "http://+:80/" so it catches every Host header.
//     This needs admin (the service runs as LocalSystem, which has it). If port
//     80 is already taken by another listener (IIS, Skype legacy), startup is
//     non-fatal — we just log a warning and the hosts-file fallback turns into
//     a generic "ERR_CONNECTION_REFUSED" page, which still effectively blocks.
//   * HTTPS is intentionally not intercepted — that would require installing a
//     trusted CA and is way out of scope. HTTPS-only sites still get blocked
//     because the TLS handshake on 127.0.0.1:443 fails, but the browser shows
//     its own error page.
[SupportedOSPlatform("windows")]
public sealed class LocalBlockedPageServer : BackgroundService
{
    private readonly ServiceState _state;
    private readonly TimeProvider _clock;
    private readonly ILogger<LocalBlockedPageServer> _logger;
    private HttpListener? _listener;

    public LocalBlockedPageServer(ServiceState state, TimeProvider clock, ILogger<LocalBlockedPageServer> logger)
    {
        _state = state;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!HttpListener.IsSupported)
        {
            _logger.LogWarning("HttpListener is not supported on this platform; blocked-page server disabled.");
            return;
        }

        _listener = new HttpListener();
        _listener.Prefixes.Add("http://+:80/");
        try
        {
            _listener.Start();
            _logger.LogInformation("LocalBlockedPageServer listening on http://+:80/");
        }
        catch (HttpListenerException ex)
        {
            // 5 = ERROR_ACCESS_DENIED (need a urlacl reservation or admin)
            // 32 = ERROR_SHARING_VIOLATION (something else owns :80)
            _logger.LogWarning(ex, "Could not bind http://+:80/ ; redirected sites will show a generic ERR_CONNECTION_REFUSED page.");
            return;
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = await _listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException) { break; }
                catch (HttpListenerException) { break; }
                catch (InvalidOperationException) { break; }

                _ = Task.Run(() => HandleRequestAsync(ctx, stoppingToken), stoppingToken);
            }
        }
        finally
        {
            try { _listener?.Stop(); } catch (ObjectDisposedException) { }
            try { _listener?.Close(); } catch (ObjectDisposedException) { }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx, CancellationToken ct)
    {
        try
        {
            var host = ctx.Request.Url?.Host ?? ctx.Request.Headers["Host"] ?? "this site";
            host = host.Split(':')[0];
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)) host = host[4..];

            var endsAtUtc = NextActiveSessionEnd();
            var endsAtLocal = endsAtUtc?.ToLocalTime();
            var endsAtText = endsAtLocal is null
                ? "the session ends"
                : endsAtLocal.Value.ToString("h:mm tt", System.Globalization.CultureInfo.InvariantCulture);

            var html = BuildPage(host, endsAtText);
            var bytes = Encoding.UTF8.GetBytes(html);

            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "text/html; charset=utf-8";
            ctx.Response.Headers.Add("Cache-Control", "no-store, must-revalidate");
            ctx.Response.Headers.Add("Pragma", "no-cache");
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Blocked-page response failed.");
        }
        finally
        {
            try { ctx.Response.Close(); } catch { /* socket already gone */ }
        }
    }

    private DateTime? NextActiveSessionEnd()
    {
        var snapshot = _state.Current;
        if (snapshot is null || snapshot.ActiveSessions.Count == 0) return null;
        return snapshot.ActiveSessions.Min(s => s.EndAtUtc);
    }

    // Mirrors design_handoff_stopme/prototype — serif headline + violet shield
    // + accent pill + italic quote. Inline styles (no external assets needed).
    private static string BuildPage(string host, string endsAtText)
    {
        var safeHost = System.Net.WebUtility.HtmlEncode(host);
        var safeEnds = System.Net.WebUtility.HtmlEncode(endsAtText);
        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<title>Blocked by StopMe</title>
<meta name="viewport" content="width=device-width, initial-scale=1" />
<style>
  :root {
    --accent: #7C5CFF;
    --accent-active: #5C3CE0;
    --accent-soft: #F1ECFF;
    --accent-soft-2: #E4DCFF;
    --accent-text: #6442E8;
    --bg: #FFFFFF;
    --text-primary: #17151F;
    --text-secondary: #6E6A7C;
    --text-tertiary: #9B97AA;
  }
  html, body { margin: 0; min-height: 100%; }
  body {
    font-family: "Schibsted Grotesk", "Segoe UI Variable Display", "Segoe UI", system-ui, sans-serif;
    color: var(--text-primary);
    background: linear-gradient(170deg, #FBFAFE 0%, var(--accent-soft) 100%);
  }
  main {
    max-width: 480px; margin: 0 auto; padding: 80px 24px; min-height: 100vh;
    display: flex; flex-direction: column; align-items: center; justify-content: center;
    text-align: center; gap: 14px;
  }
  .shield {
    width: 64px; height: 64px; border-radius: 20px; margin-bottom: 8px;
    background: linear-gradient(140deg, var(--accent), var(--accent-active));
    color: #fff; display: flex; align-items: center; justify-content: center;
    box-shadow: 0 8px 24px rgba(109,90,230,0.35);
  }
  h1 {
    font-family: "Instrument Serif", Cambria, Georgia, serif;
    font-size: 40px; font-weight: 400; line-height: 1.1; letter-spacing: -0.01em;
    color: var(--text-primary); margin: 0;
  }
  p.lead { font-size: 15px; line-height: 1.5; max-width: 400px; color: var(--text-secondary); margin: 0; }
  p.lead strong { color: var(--text-primary); font-weight: 600; }
  .pill {
    margin-top: 14px; padding: 10px 22px; border-radius: 999px; background: var(--bg);
    border: 1px solid var(--accent-soft-2); display: inline-flex; align-items: center; gap: 8px;
    font-size: 13.5px; font-weight: 600; color: var(--accent-text);
  }
  .quote {
    margin-top: 22px; font-family: "Instrument Serif", Cambria, Georgia, serif;
    font-style: italic; font-size: 17px; color: var(--text-tertiary); max-width: 440px;
  }
  .meta {
    margin-top: 12px; font-size: 12px; color: var(--text-tertiary); letter-spacing: 0.04em;
    text-transform: uppercase;
  }
</style>
</head>
<body>
<main>
  <div class="shield" aria-hidden="true">
    <svg width="36" height="36" viewBox="0 0 24 24" fill="none" stroke="currentColor"
         stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round">
      <path d="M12 3l7.5 3v5.5c0 4.6-3.1 7.9-7.5 9.5-4.4-1.6-7.5-4.9-7.5-9.5V6l7.5-3z" />
    </svg>
  </div>
  <h1>This one can wait.</h1>
  <p class="lead">
    <strong>{{safeHost}}</strong> is blocked while StopMe protects your focus.
    It will be available again at <strong>{{safeEnds}}</strong>.
  </p>
  <div class="pill">
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor"
         stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round">
      <circle cx="12" cy="12" r="8.5" />
      <path d="M12 7v5.2l3.4 2" />
    </svg>
    <span>Back to it at {{safeEnds}}</span>
  </div>
  <p class="quote">"You will never reach your destination if you stop and throw stones at every dog that barks."</p>
  <p class="meta">Served by StopMe local guard</p>
</main>
</body>
</html>
""";
    }
}
