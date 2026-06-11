using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StopMe.Core.Engines;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.Ipc;
using StopMe.Service.Tests.Fakes;
using Xunit;

namespace StopMe.Service.Tests.Ipc;

public class Phase6HandlerTests
{
    [Fact]
    public async Task StatsSummary_returns_aggregated_window()
    {
        await using var host = new TestHost(nowUtc: new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc));
        var now = host.Clock.GetUtcNow().UtcDateTime;

        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.UsageEvents.Add(new UsageEvent { Type = UsageEventType.WebsiteBlocked, DisplayLabel = "youtube.com", OccurredAtUtc = now.AddHours(-1) });
            db.UsageEvents.Add(new UsageEvent { Type = UsageEventType.WebsiteBlocked, DisplayLabel = "youtube.com", OccurredAtUtc = now.AddHours(-2) });
            db.UsageEvents.Add(new UsageEvent { Type = UsageEventType.AppBlocked,     DisplayLabel = "steam.exe",  OccurredAtUtc = now.AddHours(-3) });
            db.FocusSessions.Add(new FocusSession
            {
                Id = Guid.NewGuid(),
                UserProfileId = DefaultUserContext.DefaultId,
                Name = "Morning",
                StartAtUtc = now.AddHours(-2),
                EndAtUtc = now.AddHours(-1),
                Status = SessionStatus.Completed,
                Mode = SessionMode.Soft,
                LockPolicy = LockPolicy.None,
                TimeZoneId = "UTC",
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
            await db.SaveChangesAsync();
        }

        var resp = await DispatchAsync(host, Operations.StatsSummary, new StatsQueryRequest { Period = "Today" });
        resp.Success.Should().BeTrue();
        var dto = resp.PayloadAs<StatsSummaryResponse>()!;
        dto.Summary.WebsiteBlockCount.Should().Be(2);
        dto.Summary.AppBlockCount.Should().Be(1);
        dto.Summary.SessionsCompleted.Should().Be(1);
        dto.TopDistractions.Should().NotBeEmpty();
        dto.PrivacyMode.Should().Be("Readable");
    }

    [Fact]
    public async Task StatsSummary_returns_empty_when_privacy_disabled()
    {
        await using var host = new TestHost(nowUtc: new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc));

        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.Settings.Add(new Setting
            {
                Key = UsageEventRecorder.PolicySettingKey,
                Value = StatsRetentionPolicy.Disabled.ToString(),
                UpdatedAtUtc = host.Clock.GetUtcNow().UtcDateTime,
            });
            db.UsageEvents.Add(new UsageEvent { Type = UsageEventType.WebsiteBlocked, DisplayLabel = "youtube.com", OccurredAtUtc = host.Clock.GetUtcNow().UtcDateTime });
            await db.SaveChangesAsync();
        }

        var resp = await DispatchAsync(host, Operations.StatsSummary, new StatsQueryRequest { Period = "Today" });
        var dto = resp.PayloadAs<StatsSummaryResponse>()!;
        dto.PrivacyMode.Should().Be("Disabled");
        dto.Summary.WebsiteBlockCount.Should().Be(0);
        dto.Daily.Should().BeEmpty();
    }

    [Fact]
    public async Task StatsExport_returns_csv_text_with_filename()
    {
        await using var host = new TestHost();
        var resp = await DispatchAsync(host, Operations.StatsExportCsv, new StatsExportRequest { Period = "Last7Days" });
        var dto = resp.PayloadAs<StatsExportResponse>()!;
        dto.Filename.Should().StartWith("stopme-stats-");
        dto.CsvText.Should().Contain("section,date,focus_minutes");
    }

    [Fact]
    public async Task StatsReset_wipes_usage_events_only()
    {
        await using var host = new TestHost();
        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.UsageEvents.Add(new UsageEvent { Type = UsageEventType.WebsiteBlocked, OccurredAtUtc = host.Clock.GetUtcNow().UtcDateTime });
            db.UsageEvents.Add(new UsageEvent { Type = UsageEventType.AppBlocked,     OccurredAtUtc = host.Clock.GetUtcNow().UtcDateTime });
            db.Blocklists.Add(new Blocklist
            {
                Id = Guid.NewGuid(),
                UserProfileId = DefaultUserContext.DefaultId,
                Name = "Keep",
                Mode = BlocklistMode.Blacklist,
                CreatedAtUtc = host.Clock.GetUtcNow().UtcDateTime,
                UpdatedAtUtc = host.Clock.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync();
        }

        var resp = await DispatchRawAsync(host, Operations.StatsReset);
        var dto = resp.PayloadAs<StatsResetResponse>()!;
        dto.DeletedEvents.Should().Be(2);

        await using var s = host.CreateScope();
        var db2 = s.ServiceProvider.GetRequiredService<StopMeDbContext>();
        (await db2.UsageEvents.CountAsync()).Should().Be(0);
        (await db2.Blocklists.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task SettingsSet_persists_value_and_get_returns_it()
    {
        await using var host = new TestHost();

        var set = await DispatchAsync(host, Operations.SettingsSet,
            new SettingsSetRequest { Key = "stats.privacy_mode", Value = "Anonymized" });
        set.Success.Should().BeTrue();

        var get = await DispatchAsync(host, Operations.SettingsGet,
            new SettingsGetRequest { Key = "stats.privacy_mode" });
        var dto = get.PayloadAs<SettingsGetResponse>()!;
        dto.Entries.Should().ContainKey("stats.privacy_mode").WhoseValue.Should().Be("Anonymized");
    }

    [Fact]
    public async Task EvaluateUrl_records_website_blocked_event_on_block_decision()
    {
        await using var host = new TestHost(nowUtc: new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc));
        var nowUtc = host.Clock.GetUtcNow().UtcDateTime;

        host.Services.GetRequiredService<global::StopMe.Service.State.ServiceState>().UpdateSnapshot(
            new global::StopMe.Service.State.RuleSnapshot(
                nowUtc,
                Array.Empty<global::StopMe.Core.ValueObjects.ActiveSessionInstance>(),
                new[]
                {
                    new global::StopMe.Core.ValueObjects.CompiledRule
                    {
                        RuleId = Guid.NewGuid(),
                        BlocklistId = Guid.NewGuid(),
                        Type = RuleType.Domain,
                        Value = "youtube.com",
                        SourceMode = BlocklistMode.Blacklist,
                    },
                }),
            nowUtc);

        var resp = await DispatchAsync(host, Operations.EvaluateUrl,
            new EvaluateUrlRequest { Url = "https://youtube.com/watch" });
        resp.Success.Should().BeTrue();

        await using var s = host.CreateScope();
        var db = s.ServiceProvider.GetRequiredService<StopMeDbContext>();
        var events = await db.UsageEvents.ToListAsync();
        events.Should().ContainSingle(e => e.Type == UsageEventType.WebsiteBlocked && e.DisplayLabel == "youtube.com");
    }

    private static Task<LocalApiResponse> DispatchRawAsync(TestHost host, string op) =>
        host.Services.GetRequiredService<LocalApiDispatcher>()
            .DispatchAsync(LocalApiRequest.For(op, host.Token), default);

    private static Task<LocalApiResponse> DispatchAsync<T>(TestHost host, string op, T payload) =>
        host.Services.GetRequiredService<LocalApiDispatcher>()
            .DispatchAsync(LocalApiRequest.For(op, payload, host.Token), default);
}
