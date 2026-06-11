using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using StopMe.Core.Abstractions;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.Tests.Fakes;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class TemplateServiceTests
{
    private readonly FakeBlocklistRepository _blocklists = new();
    private readonly FakeSessionRepository _sessions = new();
    private readonly FakeCategoryRepository _categories = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 8, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public void Defaults_expose_six_templates()
    {
        var svc = Build();
        var keys = svc.List().Select(t => t.Key).ToArray();
        keys.Should().BeEquivalentTo(new[] { "study", "work", "sleep", "exam", "deep-work", "writing" });
    }

    [Fact]
    public void FindByKey_is_case_insensitive()
    {
        var svc = Build();
        svc.FindByKey("Study").Should().NotBeNull();
        svc.FindByKey("STUDY").Should().NotBeNull();
        svc.FindByKey("unknown").Should().BeNull();
    }

    [Fact]
    public async Task Apply_unknown_template_returns_error()
    {
        var svc = Build();
        var result = await svc.ApplyAsync("nope", _clock.GetUtcNow().UtcDateTime, Guid.NewGuid());
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Unknown template");
    }

    [Fact]
    public async Task Apply_study_creates_blocklist_and_session()
    {
        _categories.Add("Social Media", new[] { "instagram.com", "tiktok.com" });
        _categories.Add("Video Streaming", new[] { "youtube.com" });
        _categories.Add("Gaming", new[] { "steam.com" });
        _categories.Add("Entertainment", new[] { "netflix.com" });

        var svc = Build();
        var userId = Guid.NewGuid();
        var startUtc = _clock.GetUtcNow().UtcDateTime;

        var result = await svc.ApplyAsync("study", startUtc, userId);

        result.Success.Should().BeTrue();
        result.BlocklistIds.Should().HaveCount(1);
        result.SessionId.Should().NotBeNull();

        // 2 (Social Media) + 1 (Video Streaming) + 1 (Gaming) + 1 (Entertainment) = 5 rules
        _blocklists.Stored.Should().ContainSingle().Which.Rules.Should().HaveCount(5);
        var session = _sessions.Sessions.Single();
        session.UserProfileId.Should().Be(userId);
        session.Mode.Should().Be(SessionMode.Soft);
        session.EndAtUtc.Should().Be(startUtc.AddHours(2));
        session.SessionBlocklists.Should().ContainSingle();
    }

    [Fact]
    public async Task Apply_exam_creates_allowlist_with_exception_domain_rules()
    {
        var svc = Build();
        var result = await svc.ApplyAsync("exam", _clock.GetUtcNow().UtcDateTime, Guid.NewGuid());

        result.Success.Should().BeTrue();
        var bl = _blocklists.Stored.Single();
        bl.Mode.Should().Be(BlocklistMode.Allowlist);
        bl.Rules.Should().NotBeEmpty();
        bl.Rules.Should().OnlyContain(r => r.Type == RuleType.ExceptionDomain);
    }

    [Fact]
    public async Task Apply_work_creates_recurring_session()
    {
        var svc = Build();
        var result = await svc.ApplyAsync("work", _clock.GetUtcNow().UtcDateTime, Guid.NewGuid());

        result.Success.Should().BeTrue();
        var session = _sessions.Sessions.Single();
        session.IsRecurring.Should().BeTrue();
        session.RecurrenceRule.Should().Be("FREQ=WEEKLY;BYDAY=MO,TU,WE,TH,FR");
    }

    private TemplateService Build() =>
        new(_blocklists, _sessions, _categories, _clock);

    private sealed class FakeBlocklistRepository : IBlocklistRepository
    {
        public List<Blocklist> Stored { get; } = new();

        public Task<IReadOnlyList<Blocklist>> GetAllAsync(Guid userProfileId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Blocklist>>(Stored.Where(b => b.UserProfileId == userProfileId).ToList());

        public Task<IReadOnlyList<Blocklist>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Blocklist>>(Stored.Where(b => ids.Contains(b.Id)).ToList());

        public Task<Blocklist?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
            Task.FromResult(Stored.FirstOrDefault(b => b.Id == id));

        public Task AddAsync(Blocklist blocklist, CancellationToken ct = default)
        {
            Stored.Add(blocklist);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Blocklist blocklist, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    
    public Task<int> AppendRulesAsync(Guid blocklistId, IReadOnlyList<BlockRule> rules, DateTime updatedAtUtc, CancellationToken ct = default) => Task.FromResult(rules.Count);
}

    private sealed class FakeCategoryRepository : ICategoryRepository
    {
        private readonly Dictionary<string, IReadOnlyList<string>> _store = new(StringComparer.OrdinalIgnoreCase);

        public void Add(string category, IReadOnlyList<string> domains) => _store[category] = domains;

        public Task<IReadOnlyList<string>> GetDomainsAsync(string categoryName, CancellationToken ct = default) =>
            Task.FromResult(_store.TryGetValue(categoryName, out var d) ? d : Array.Empty<string>());

        public Task<IReadOnlyList<string>> GetCategoryNamesAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<string>>(_store.Keys.ToArray());
    }
}
