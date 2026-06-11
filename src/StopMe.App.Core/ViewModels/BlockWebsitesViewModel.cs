using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Services;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.ViewModels;

// One-stop "block these websites now" page. Mirrors BlockAppsViewModel but for
// URL/domain rules. Lets the user add specific domains / wildcards, pick quick
// category groups (social/video/gaming/news/shopping/AI), set exceptions, then
// create + start a session.
//
// Categories are demo data for now (TODO: surface the service-side CategoryDomains
// seed via a categories.list IPC op so the chip counts stay in sync with what
// actually gets blocked). The Selected / Exceptions lists are the real source of
// truth that drives session creation.
public partial class BlockWebsitesViewModel : ViewModelBase
{
    public static readonly int[] AvailableDurationsMinutes = { 15, 30, 60, 120, 240 };
    public static readonly string[] AvailableModes = { "Soft", "Strict" };

    private readonly IServiceConnection _connection;
    private readonly TimeProvider _clock;

    public BlockWebsitesViewModel(IServiceConnection connection, TimeProvider clock, IDispatcher? dispatcher = null)
        : base(dispatcher)
    {
        _connection = connection;
        _clock = clock;
        _durationMinutes = 60;
        _selectedMode = "Soft";
        _siteInput = string.Empty;
        _exceptionInput = string.Empty;

        // Seed a few sensible defaults — these mirror the prototype demo data.
        SelectedSites.Add("youtube.com");
        SelectedSites.Add("reddit.com");
        SelectedCategories.Add("social");
        Exceptions.Add("docs.google.com");

        Categories.Add(new SiteCategory("social",   "Social Media",   "globe",   new[] { "facebook.com", "instagram.com", "twitter.com", "x.com", "tiktok.com", "reddit.com", "linkedin.com/feed" }));
        Categories.Add(new SiteCategory("video",    "Video Streaming", "play",   new[] { "youtube.com", "netflix.com", "twitch.tv", "primevideo.com", "disneyplus.com" }));
        Categories.Add(new SiteCategory("gaming",   "Gaming",         "zap",     new[] { "store.steampowered.com", "epicgames.com", "op.gg", "ign.com" }));
        Categories.Add(new SiteCategory("news",     "News",           "file",    new[] { "cnn.com", "bbc.com", "lemonde.fr", "news.google.com" }));
        Categories.Add(new SiteCategory("shopping", "Shopping",       "apps",    new[] { "amazon.com", "ebay.com", "aliexpress.com", "vinted.fr" }));
        Categories.Add(new SiteCategory("ai",       "AI Chatbots",    "sparkle", new[] { "chatgpt.com", "claude.ai", "gemini.google.com", "character.ai" }));
    }

    public ObservableCollection<string> SelectedSites { get; } = new();
    public ObservableCollection<string> SelectedCategories { get; } = new();
    public ObservableCollection<string> Exceptions { get; } = new();
    public ObservableCollection<SiteCategory> Categories { get; } = new();
    public ObservableCollection<DomainSearchHitDto> SearchResults { get; } = new();

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    public IReadOnlyList<int> AvailableDurations { get; } = AvailableDurationsMinutes;

    [ObservableProperty]
    private string _siteInput;

    [ObservableProperty]
    private string _exceptionInput;

    [ObservableProperty]
    private int _durationMinutes;

    [ObservableProperty]
    private string _selectedMode;

    [ObservableProperty]
    private string? _lastStartSummary;

    public int CategorySiteCount =>
        SelectedCategories.Sum(id => Categories.FirstOrDefault(c => c.Id == id)?.Sites.Count ?? 0);

    public int TotalCount => SelectedSites.Count + CategorySiteCount;

    public bool HasSelection => TotalCount > 0;

    public bool HasNoSites => SelectedSites.Count == 0;

    [RelayCommand]
    public void AddSite()
    {
        var raw = SiteInput?.Trim().ToLowerInvariant() ?? string.Empty;
        if (raw.StartsWith("https://")) raw = raw[8..];
        else if (raw.StartsWith("http://")) raw = raw[7..];
        if (raw.StartsWith("www.")) raw = raw[4..];
        if (string.IsNullOrEmpty(raw) || SelectedSites.Contains(raw)) { SiteInput = string.Empty; return; }
        SelectedSites.Add(raw);
        SiteInput = string.Empty;
        Notify();
    }

    [RelayCommand]
    public void RemoveSite(string site)
    {
        if (site is null) return;
        SelectedSites.Remove(site);
        Notify();
    }

    [RelayCommand]
    public void ToggleCategory(string id)
    {
        if (id is null) return;
        if (!SelectedCategories.Remove(id)) SelectedCategories.Add(id);
        Notify();
    }

    [RelayCommand]
    public void AddException()
    {
        var raw = ExceptionInput?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(raw) || Exceptions.Contains(raw)) { ExceptionInput = string.Empty; return; }
        Exceptions.Add(raw);
        ExceptionInput = string.Empty;
    }

    // Keyword → domains lookup. Hits the service's IDomainSearchProvider, which
    // — when wired to the local heuristic provider — never leaves the machine.
    // The user can then "Add" each result into their blocklist.
    [RelayCommand]
    public async Task SearchAsync(CancellationToken ct = default)
    {
        var q = SearchQuery?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(q)) return;
        ClearError();
        IsSearching = true;
        try
        {
            var hits = await _connection.SearchDomainsAsync(q, 12, ct).ConfigureAwait(false);
            await RunOnUiAsync(() =>
            {
                SearchResults.Clear();
                foreach (var h in hits)
                {
                    if (SelectedSites.Contains(h.Domain, StringComparer.OrdinalIgnoreCase)) continue;
                    SearchResults.Add(h);
                }
                StatusMessage = SearchResults.Count == 0
                    ? $"No results found for '{q}'."
                    : $"{SearchResults.Count} domain(s) found.";
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsSearching = false).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public void AddFromSearch(DomainSearchHitDto hit)
    {
        if (hit is null) return;
        var d = HostsFriendly(hit.Domain);
        if (!SelectedSites.Contains(d, StringComparer.OrdinalIgnoreCase))
        {
            SelectedSites.Add(d);
        }
        SearchResults.Remove(hit);
        Notify();
    }

    private static string HostsFriendly(string d)
    {
        var v = d.Trim().ToLowerInvariant()
            .Replace("https://", "", StringComparison.Ordinal)
            .Replace("http://", "", StringComparison.Ordinal);
        // TrimStart('w','.') was a bug: it trims any leading 'w' or '.' chars,
        // so "wired.com" becomes "ired.com". Strip the exact "www." prefix.
        return v.StartsWith("www.", StringComparison.Ordinal) ? v[4..] : v;
    }

    [RelayCommand]
    public void RemoveException(string site)
    {
        if (site is null) return;
        Exceptions.Remove(site);
    }

    [RelayCommand]
    public async Task StartBlockingAsync(CancellationToken ct = default)
    {
        ClearError();
        var allSites = SelectedSites
            .Concat(SelectedCategories.SelectMany(id => Categories.FirstOrDefault(c => c.Id == id)?.Sites ?? Array.Empty<string>()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (allSites.Length == 0)
        {
            SetError("Select at least one website (or pick a category) before starting.");
            return;
        }
        if (DurationMinutes <= 0)
        {
            SetError("Pick a positive duration.");
            return;
        }

        IsBusy = true;
        try
        {
            var rules = allSites.Select(s => new CreateRuleRequest
            {
                // "UrlPattern" was a typo — the enum value is Wildcard, and
                // the service-side parser would reject the rule outright.
                Type = s.Contains('*') ? "Wildcard" : "Domain",
                Value = s,
            }).ToList();
            rules.AddRange(Exceptions.Select(e => new CreateRuleRequest
            {
                Type = "ExceptionDomain",
                Value = e,
            }));

            var blocklist = await _connection.CreateBlocklistAsync(new CreateBlocklistRequest
            {
                Name = $"Quick web block — {_clock.GetUtcNow().UtcDateTime:yyyy-MM-dd HH:mm}",
                Description = $"Auto-generated for: {string.Join(", ", allSites.Take(5))}{(allSites.Length > 5 ? "…" : "")}",
                Mode = "Blacklist",
                Rules = rules,
            }, ct).ConfigureAwait(false);
            if (blocklist is null)
            {
                SetError("Could not create blocklist. " + (_connection.LastError ?? string.Empty));
                return;
            }

            var startAtUtc = _clock.GetUtcNow().UtcDateTime;
            var endAtUtc = startAtUtc.AddMinutes(DurationMinutes);

            var lockPolicy = string.Equals(SelectedMode, "Strict", StringComparison.OrdinalIgnoreCase)
                ? "TimeLocked"
                : "Confirmation";

            var session = await _connection.CreateSessionAsync(new CreateSessionRequest
            {
                Name = "Web focus",
                StartAtUtc = startAtUtc,
                EndAtUtc = endAtUtc,
                Mode = SelectedMode,
                LockPolicy = lockPolicy,
                BlocklistIds = new[] { blocklist.Id },
                StartImmediately = true,
            }, ct).ConfigureAwait(false);

            if (session is null)
            {
                SetError("Blocklist created but session could not start. " + (_connection.LastError ?? string.Empty));
                return;
            }

            await RunOnUiAsync(() =>
            {
                LastStartSummary = $"Blocking {allSites.Length} site(s). Ends at {endAtUtc.ToLocalTime():HH:mm}.";
                StatusMessage = LastStartSummary;
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }

    private void Notify()
    {
        OnPropertyChanged(nameof(CategorySiteCount));
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasNoSites));
    }
}

public sealed record SiteCategory(string Id, string Label, string Icon, IReadOnlyList<string> Sites);
