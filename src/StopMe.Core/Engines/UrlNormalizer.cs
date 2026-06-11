namespace StopMe.Core.Engines;

public static class UrlNormalizer
{
    public static NormalizedUrl Normalize(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            throw new FormatException("URL cannot be empty.");

        var s = rawUrl.Trim();

        if (!Uri.TryCreate(s, UriKind.Absolute, out var uri))
        {
            // Allow scheme-less inputs by retrying with https://
            if (!Uri.TryCreate("https://" + s, UriKind.Absolute, out uri))
                throw new FormatException($"Invalid URL: '{rawUrl}'.");
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme is not "http" and not "https")
            throw new FormatException($"Unsupported scheme '{scheme}'.");

        var host = uri.IdnHost.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(host))
            throw new FormatException($"URL '{rawUrl}' has no host.");

        var path = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath;
        var query = string.IsNullOrEmpty(uri.Query) ? null : uri.Query;

        int? port = uri.IsDefaultPort ? null : uri.Port;

        return new NormalizedUrl
        {
            Scheme = scheme,
            Host = host,
            Port = port,
            Path = path,
            Query = query,
        };
    }

    public static bool TryNormalize(string rawUrl, out NormalizedUrl? url)
    {
        try
        {
            url = Normalize(rawUrl);
            return true;
        }
        catch (FormatException)
        {
            url = null;
            return false;
        }
    }
}
