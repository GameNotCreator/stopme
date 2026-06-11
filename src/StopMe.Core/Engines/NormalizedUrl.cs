namespace StopMe.Core.Engines;

public sealed class NormalizedUrl
{
    public required string Scheme { get; init; }

    public required string Host { get; init; }

    public int? Port { get; init; }

    public required string Path { get; init; }

    public string? Query { get; init; }

    public string HostPathQuery => Host + Path + (Query ?? string.Empty);

    public string FullForm =>
        Port.HasValue
            ? $"{Scheme}://{Host}:{Port.Value}{Path}{Query}"
            : $"{Scheme}://{Host}{Path}{Query}";

    public override string ToString() => FullForm;
}
