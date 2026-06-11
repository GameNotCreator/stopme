using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;
using StopMe.Infrastructure.Repositories;
using Xunit;

namespace StopMe.Core.Tests.Infrastructure;

public class Phase6RepositoryTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<StopMeDbContext> _options;
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 8, 10, 0, 0, TimeSpan.Zero));

    public Phase6RepositoryTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _options = new DbContextOptionsBuilder<StopMeDbContext>().UseSqlite(_conn).Options;

        using var ctx = new StopMeDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    [Fact]
    public async Task SettingsRepository_round_trips_keys()
    {
        await using var ctx = new StopMeDbContext(_options);
        var repo = new SettingsRepository(ctx, _clock);

        await repo.SetAsync("stats.privacy_mode", "Anonymized");
        (await repo.GetAsync("stats.privacy_mode")).Should().Be("Anonymized");

        await repo.SetAsync("stats.privacy_mode", "Disabled");
        (await repo.GetAsync("stats.privacy_mode")).Should().Be("Disabled");

        var all = await repo.GetAllAsync();
        all.Should().ContainKey("stats.privacy_mode");
    }

    [Fact]
    public async Task UsageEventRepository_returns_events_in_window_only()
    {
        await Seed(new UsageEvent { Type = UsageEventType.WebsiteBlocked, OccurredAtUtc = new(2026, 6, 6, 8, 0, 0, DateTimeKind.Utc) });
        await Seed(new UsageEvent { Type = UsageEventType.WebsiteBlocked, OccurredAtUtc = new(2026, 6, 7, 8, 0, 0, DateTimeKind.Utc) });
        await Seed(new UsageEvent { Type = UsageEventType.WebsiteBlocked, OccurredAtUtc = new(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc) });

        await using var ctx = new StopMeDbContext(_options);
        var repo = new UsageEventRepository(ctx);

        var got = await repo.GetBetweenAsync(
            new DateTime(2026, 6, 6, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc));

        got.Should().HaveCount(2);
    }

    [Fact]
    public async Task UsageEventRepository_DeleteAll_removes_every_row()
    {
        await Seed(new UsageEvent { Type = UsageEventType.WebsiteBlocked, OccurredAtUtc = _clock.GetUtcNow().UtcDateTime });
        await Seed(new UsageEvent { Type = UsageEventType.AppBlocked,     OccurredAtUtc = _clock.GetUtcNow().UtcDateTime });

        await using var ctx = new StopMeDbContext(_options);
        var repo = new UsageEventRepository(ctx);
        var deleted = await repo.DeleteAllAsync();

        deleted.Should().Be(2);

        await using var ctx2 = new StopMeDbContext(_options);
        (await ctx2.UsageEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SessionRepository_GetCompletedBetween_filters_by_status_and_endAt()
    {
        var userId = Guid.NewGuid();
        async Task SeedSession(SessionStatus status, DateTime endUtc)
        {
            await using var ctx = new StopMeDbContext(_options);
            ctx.FocusSessions.Add(new FocusSession
            {
                Id = Guid.NewGuid(),
                UserProfileId = userId,
                Name = "S",
                StartAtUtc = endUtc.AddHours(-1),
                EndAtUtc = endUtc,
                Status = status,
                Mode = SessionMode.Soft,
                LockPolicy = LockPolicy.None,
                TimeZoneId = "UTC",
                CreatedAtUtc = endUtc,
                UpdatedAtUtc = endUtc,
            });
            await ctx.SaveChangesAsync();
        }

        await SeedSession(SessionStatus.Completed, new DateTime(2026, 6, 5, 10, 0, 0, DateTimeKind.Utc));
        await SeedSession(SessionStatus.Completed, new DateTime(2026, 6, 6, 10, 0, 0, DateTimeKind.Utc));
        await SeedSession(SessionStatus.Cancelled, new DateTime(2026, 6, 6, 11, 0, 0, DateTimeKind.Utc));
        await SeedSession(SessionStatus.Completed, new DateTime(2026, 6, 10, 10, 0, 0, DateTimeKind.Utc));

        await using var ctx = new StopMeDbContext(_options);
        var repo = new SessionRepository(ctx);
        var got = await repo.GetCompletedBetweenAsync(
            new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 8, 0, 0, 0, DateTimeKind.Utc));

        got.Should().HaveCount(2);
        got.Should().OnlyContain(s => s.Status == SessionStatus.Completed);
    }

    [Fact]
    public async Task BypassEventRepository_CountBetween_excludes_outside_window()
    {
        async Task Add(BypassEventType type, DateTime atUtc)
        {
            await using var ctx = new StopMeDbContext(_options);
            ctx.BypassEvents.Add(new BypassEvent { Type = type, DetectedAtUtc = atUtc });
            await ctx.SaveChangesAsync();
        }

        await Add(BypassEventType.ClockDrift, new(2026, 6, 5, 8, 0, 0, DateTimeKind.Utc));
        await Add(BypassEventType.ClockDrift, new(2026, 6, 6, 8, 0, 0, DateTimeKind.Utc));
        await Add(BypassEventType.ExtensionStale, new(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc));

        await using var ctx = new StopMeDbContext(_options);
        var repo = new BypassEventRepository(ctx);

        var count = await repo.CountBetweenAsync(
            new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 7, 0, 0, 0, DateTimeKind.Utc));
        count.Should().Be(2);

        var got = await repo.GetBetweenAsync(
            new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 6, 7, 0, 0, 0, DateTimeKind.Utc),
            limit: 10);
        got.Should().HaveCount(2);
    }

    private async Task Seed(UsageEvent evt)
    {
        await using var ctx = new StopMeDbContext(_options);
        ctx.UsageEvents.Add(evt);
        await ctx.SaveChangesAsync();
    }

    public void Dispose()
    {
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
