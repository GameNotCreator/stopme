using StopMe.LocalApi.Dtos;

namespace StopMe.Service.Search;

// Offline keyword → domains lookup. Replaces the DuckDuckGo HTML provider that
// shipped through Phase 8 — DDG made every "Search domains" click leak the
// user's typed query off the machine, in direct contradiction with the
// "nothing leaves the machine" promise on the box. The new provider walks a
// small curated category list keyed by user-recognisable terms (gaming,
// social, news, …) and matches the query against keywords + domain
// substrings. It is intentionally small — the user can still type any domain
// directly into the "Block specific site" field.
public sealed class LocalDomainSearchProvider : IDomainSearchProvider
{
    public string Name => "Local";

    public Task<IReadOnlyList<DomainSearchHitDto>> SearchAsync(string query, int maxResults, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Task.FromResult<IReadOnlyList<DomainSearchHitDto>>(Array.Empty<DomainSearchHitDto>());

        var max = Math.Clamp(maxResults, 1, 30);
        var q = query.Trim().ToLowerInvariant();
        var seenDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hits = new List<DomainSearchHitDto>();

        foreach (var entry in Catalogue)
        {
            if (hits.Count >= max) break;

            var matches =
                entry.Keywords.Any(k => k.Contains(q, StringComparison.OrdinalIgnoreCase))
                || q.Contains(entry.Category, StringComparison.OrdinalIgnoreCase)
                || entry.Domain.Contains(q, StringComparison.OrdinalIgnoreCase)
                || entry.Title.Contains(q, StringComparison.OrdinalIgnoreCase);
            if (!matches) continue;

            if (!seenDomains.Add(entry.Domain)) continue;
            hits.Add(new DomainSearchHitDto
            {
                Domain = entry.Domain,
                Title = entry.Title,
                Url = "https://" + entry.Domain,
            });
        }

        return Task.FromResult<IReadOnlyList<DomainSearchHitDto>>(hits);
    }

    private sealed record Entry(string Category, string Domain, string Title, string[] Keywords);

    private static readonly Entry[] Catalogue = new[]
    {
        new Entry("social", "facebook.com",        "Facebook",         new[] { "social", "feed", "facebook" }),
        new Entry("social", "instagram.com",       "Instagram",        new[] { "social", "photos", "instagram", "insta", "ig" }),
        new Entry("social", "twitter.com",         "Twitter",          new[] { "social", "tweets", "twitter" }),
        new Entry("social", "x.com",               "X (Twitter)",      new[] { "social", "tweets", "x", "twitter" }),
        new Entry("social", "tiktok.com",          "TikTok",           new[] { "social", "shortvideo", "tiktok" }),
        new Entry("social", "reddit.com",          "Reddit",           new[] { "social", "forum", "reddit" }),
        new Entry("social", "linkedin.com",        "LinkedIn",         new[] { "social", "networking", "linkedin" }),
        new Entry("social", "snapchat.com",        "Snapchat",         new[] { "social", "snap", "snapchat" }),
        new Entry("social", "discord.com",         "Discord",          new[] { "social", "chat", "discord" }),

        new Entry("video",  "youtube.com",         "YouTube",          new[] { "video", "streaming", "yt", "youtube" }),
        new Entry("video",  "netflix.com",         "Netflix",          new[] { "video", "streaming", "movies", "tv", "netflix" }),
        new Entry("video",  "twitch.tv",           "Twitch",           new[] { "video", "streaming", "gaming", "twitch" }),
        new Entry("video",  "primevideo.com",      "Prime Video",      new[] { "video", "streaming", "prime", "amazon" }),
        new Entry("video",  "disneyplus.com",      "Disney+",          new[] { "video", "streaming", "disney" }),
        new Entry("video",  "hulu.com",            "Hulu",             new[] { "video", "streaming", "hulu" }),
        new Entry("video",  "vimeo.com",           "Vimeo",            new[] { "video", "vimeo" }),

        new Entry("gaming", "store.steampowered.com", "Steam Store",   new[] { "gaming", "games", "steam" }),
        new Entry("gaming", "epicgames.com",       "Epic Games",       new[] { "gaming", "games", "epic" }),
        new Entry("gaming", "op.gg",               "OP.GG",            new[] { "gaming", "league", "lol", "opgg" }),
        new Entry("gaming", "ign.com",             "IGN",              new[] { "gaming", "news", "reviews", "ign" }),
        new Entry("gaming", "gamefaqs.gamespot.com", "GameFAQs",       new[] { "gaming", "walkthroughs", "gamefaqs" }),
        new Entry("gaming", "battle.net",          "Battle.net",       new[] { "gaming", "blizzard", "battle" }),

        new Entry("news",   "cnn.com",             "CNN",              new[] { "news", "cnn" }),
        new Entry("news",   "bbc.com",             "BBC",              new[] { "news", "bbc" }),
        new Entry("news",   "lemonde.fr",          "Le Monde",         new[] { "news", "actualités", "lemonde", "monde" }),
        new Entry("news",   "lefigaro.fr",         "Le Figaro",        new[] { "news", "figaro" }),
        new Entry("news",   "nytimes.com",         "NY Times",         new[] { "news", "nyt", "times" }),
        new Entry("news",   "news.google.com",     "Google News",      new[] { "news", "google" }),
        new Entry("news",   "hackernews.com",      "Hacker News",      new[] { "news", "hackernews", "hn", "tech" }),
        new Entry("news",   "news.ycombinator.com","Hacker News",      new[] { "news", "hackernews", "hn", "tech", "ycombinator" }),

        new Entry("shopping", "amazon.com",        "Amazon",           new[] { "shopping", "amazon", "buy" }),
        new Entry("shopping", "ebay.com",          "eBay",             new[] { "shopping", "ebay", "auctions" }),
        new Entry("shopping", "aliexpress.com",    "AliExpress",       new[] { "shopping", "aliexpress" }),
        new Entry("shopping", "etsy.com",          "Etsy",             new[] { "shopping", "etsy" }),
        new Entry("shopping", "vinted.fr",         "Vinted",           new[] { "shopping", "vinted", "clothes" }),

        new Entry("ai",     "chatgpt.com",         "ChatGPT",          new[] { "ai", "chatbot", "openai", "chatgpt", "gpt" }),
        new Entry("ai",     "claude.ai",           "Claude",           new[] { "ai", "anthropic", "claude" }),
        new Entry("ai",     "gemini.google.com",   "Gemini",           new[] { "ai", "google", "gemini", "bard" }),
        new Entry("ai",     "character.ai",        "Character.AI",     new[] { "ai", "characters", "character" }),
        new Entry("ai",     "perplexity.ai",       "Perplexity",       new[] { "ai", "search", "perplexity" }),
    };
}
