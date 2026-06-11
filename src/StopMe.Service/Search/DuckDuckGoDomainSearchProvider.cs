using System.Net.Http;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Search;

// Free, no-API-key keyword → domains lookup powered by DuckDuckGo's HTML
// endpoint (html.duckduckgo.com). We deliberately avoid the JSON Instant Answer
// API because it returns very little for arbitrary queries; the HTML lite
// endpoint mirrors actual search results, which is what we want when the user
// types "gaming" / "social network" / "manga".
//
// We parse the result anchors with a regex and dedupe by registrable domain.
// This is a best-effort wrapper — if DDG changes its markup, the regex breaks
// gracefully and the call returns an empty list (the UI shows "no hits").
public sealed class DuckDuckGoDomainSearchProvider : IDomainSearchProvider
{
    private const string Endpoint = "https://html.duckduckgo.com/html/";

    private static readonly Regex ResultAnchor = new(
        @"<a[^>]+class=""result__a""[^>]+href=""(?<href>[^""]+)""[^>]*>(?<title>.*?)</a>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static readonly Regex TagStripper = new("<[^>]+>", RegexOptions.Compiled);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DuckDuckGoDomainSearchProvider> _logger;

    public DuckDuckGoDomainSearchProvider(IHttpClientFactory httpClientFactory, ILogger<DuckDuckGoDomainSearchProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "DuckDuckGo";

    public async Task<IReadOnlyList<DomainSearchHitDto>> SearchAsync(string query, int maxResults, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<DomainSearchHitDto>();
        var max = Math.Clamp(maxResults, 1, 30);

        try
        {
            using var client = _httpClientFactory.CreateClient("ddg");
            client.Timeout = TimeSpan.FromSeconds(8);
            // DDG html requires a User-Agent or returns an empty doc.
            if (client.DefaultRequestHeaders.UserAgent.Count == 0)
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) StopMe/1.0");

            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["q"] = query.Trim(),
                    ["kl"] = "wt-wt", // no-region results
                }),
            };
            using var resp = await client.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("DuckDuckGo returned {Code} for query '{Query}'.", (int)resp.StatusCode, query);
                return Array.Empty<DomainSearchHitDto>();
            }

            var html = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return ParseHits(html, max);
        }
        catch (TaskCanceledException) { return Array.Empty<DomainSearchHitDto>(); }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "DuckDuckGo HTTP request failed.");
            return Array.Empty<DomainSearchHitDto>();
        }
    }

    internal static IReadOnlyList<DomainSearchHitDto> ParseHits(string html, int max)
    {
        var seenDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hits = new List<DomainSearchHitDto>();

        foreach (Match m in ResultAnchor.Matches(html))
        {
            if (hits.Count >= max) break;
            var href = System.Net.WebUtility.HtmlDecode(m.Groups["href"].Value);
            var title = TagStripper.Replace(m.Groups["title"].Value, string.Empty).Trim();
            title = System.Net.WebUtility.HtmlDecode(title);
            if (string.IsNullOrEmpty(href)) continue;

            // DDG sometimes wraps the link in /l/?uddg=…&kh=… — unwrap it.
            href = UnwrapDdgRedirect(href);

            if (!Uri.TryCreate(href, UriKind.Absolute, out var uri))
                continue;
            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                continue;

            var domain = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? uri.Host[4..]
                : uri.Host;
            if (!seenDomains.Add(domain)) continue;
            hits.Add(new DomainSearchHitDto
            {
                Domain = domain.ToLowerInvariant(),
                Title = string.IsNullOrEmpty(title) ? domain : title,
                Url = href,
            });
        }
        return hits;
    }

    private static string UnwrapDdgRedirect(string href)
    {
        if (href.StartsWith("//duckduckgo.com/l/", StringComparison.OrdinalIgnoreCase))
            href = "https:" + href;
        if (href.StartsWith("https://duckduckgo.com/l/", StringComparison.OrdinalIgnoreCase))
        {
            var q = href.IndexOf('?', StringComparison.Ordinal);
            if (q >= 0)
            {
                var query = System.Web.HttpUtility.ParseQueryString(href[(q + 1)..]);
                var uddg = query["uddg"];
                if (!string.IsNullOrEmpty(uddg)) return uddg;
            }
        }
        return href;
    }
}
