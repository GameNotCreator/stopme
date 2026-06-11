using FluentAssertions;
using StopMe.Core.Abstractions;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.ValueObjects;
using Xunit;

namespace StopMe.Core.Tests.Engines;

public class UsageEventRecorderTests
{
    [Fact]
    public async Task Readable_mode_stores_display_label_and_hashes()
    {
        var (recorder, events, settings) = Build();
        await settings.SetAsync(UsageEventRecorder.PolicySettingKey, StatsRetentionPolicy.Readable.ToString());

        var written = await recorder.RecordAsync(new UsageEventDraft
        {
            Type = UsageEventType.WebsiteBlocked,
            DomainLabel = "Youtube.com",
            DisplayLabel = "youtube.com",
        }, new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc));

        written.Should().BeTrue();
        events.Stored.Should().ContainSingle();
        var ev = events.Stored[0];
        ev.DisplayLabel.Should().Be("youtube.com");
        ev.DomainHash.Should().NotBeNullOrEmpty();
        ev.DomainHash.Should().Be(UsageEventRecorder.Hash("Youtube.com"));
    }

    [Fact]
    public async Task Anonymized_mode_drops_display_label_but_keeps_hashes()
    {
        var (recorder, events, settings) = Build();
        await settings.SetAsync(UsageEventRecorder.PolicySettingKey, StatsRetentionPolicy.Anonymized.ToString());

        await recorder.RecordAsync(new UsageEventDraft
        {
            Type = UsageEventType.WebsiteBlocked,
            DomainLabel = "tiktok.com",
            DisplayLabel = "tiktok.com",
        }, DateTime.UtcNow);

        var ev = events.Stored.Single();
        ev.DisplayLabel.Should().BeNull();
        ev.DomainHash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Disabled_mode_skips_writing()
    {
        var (recorder, events, settings) = Build();
        await settings.SetAsync(UsageEventRecorder.PolicySettingKey, StatsRetentionPolicy.Disabled.ToString());

        var written = await recorder.RecordAsync(new UsageEventDraft
        {
            Type = UsageEventType.AppBlocked,
            AppNameLabel = "steam.exe",
        }, DateTime.UtcNow);

        written.Should().BeFalse();
        events.Stored.Should().BeEmpty();
    }

    [Fact]
    public async Task Defaults_to_readable_when_no_setting_persisted()
    {
        var (recorder, _, _) = Build();
        var policy = await recorder.GetActivePolicyAsync();
        policy.Should().Be(StatsRetentionPolicy.Readable);
    }

    [Fact]
    public async Task Set_policy_round_trips_through_settings_store()
    {
        var (recorder, _, _) = Build();
        await recorder.SetActivePolicyAsync(StatsRetentionPolicy.Anonymized);
        (await recorder.GetActivePolicyAsync()).Should().Be(StatsRetentionPolicy.Anonymized);
    }

    private static (UsageEventRecorder, FakeUsageEventRepository, FakeSettingsRepository) Build()
    {
        var events = new FakeUsageEventRepository();
        var settings = new FakeSettingsRepository();
        return (new UsageEventRecorder(events, settings), events, settings);
    }

    private sealed class FakeUsageEventRepository : IUsageEventRepository
    {
        public List<UsageEvent> Stored { get; } = new();

        public Task AddAsync(UsageEvent usageEvent, CancellationToken ct = default)
        {
            Stored.Add(usageEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<UsageEvent>> GetBetweenAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<UsageEvent>>(Stored.Where(e => e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc).ToList());

        public Task<int> CountByTypeAsync(UsageEventType type, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) =>
            Task.FromResult(Stored.Count(e => e.Type == type && e.OccurredAtUtc >= fromUtc && e.OccurredAtUtc < toUtc));

        public Task<int> DeleteAllAsync(CancellationToken ct = default)
        {
            var n = Stored.Count;
            Stored.Clear();
            return Task.FromResult(n);
        }
    }

    private sealed class FakeSettingsRepository : ISettingsRepository
    {
        private readonly Dictionary<string, string> _store = new(StringComparer.OrdinalIgnoreCase);

        public Task<string?> GetAsync(string key, CancellationToken ct = default) =>
            Task.FromResult(_store.TryGetValue(key, out var v) ? v : null);

        public Task SetAsync(string key, string value, CancellationToken ct = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>(_store, StringComparer.OrdinalIgnoreCase));
    }
}
