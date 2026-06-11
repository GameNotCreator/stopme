using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;
using StopMe.Infrastructure.Repositories;
using StopMe.Infrastructure.Seed;
using Xunit;

namespace StopMe.Core.Tests.Infrastructure;

public class DatabaseRoundTripTests : IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly DbContextOptions<StopMeDbContext> _options;

    public DatabaseRoundTripTests()
    {
        _conn = new SqliteConnection("DataSource=:memory:");
        _conn.Open();
        _options = new DbContextOptionsBuilder<StopMeDbContext>()
            .UseSqlite(_conn)
            .Options;

        using var ctx = new StopMeDbContext(_options);
        ctx.Database.EnsureCreated();
    }

    [Fact]
    public async Task Blocklist_with_rules_roundtrips()
    {
        var userId = Guid.NewGuid();
        var blocklist = new Blocklist
        {
            Id = Guid.NewGuid(),
            UserProfileId = userId,
            Name = "Social Media",
            Mode = BlocklistMode.Blacklist,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Rules =
            {
                new BlockRule { Id = Guid.NewGuid(), Type = RuleType.Domain, Value = "youtube.com", CreatedAtUtc = DateTime.UtcNow },
                new BlockRule { Id = Guid.NewGuid(), Type = RuleType.Wildcard, Value = "*.tiktok.com", CreatedAtUtc = DateTime.UtcNow },
            },
        };

        await using (var ctx = new StopMeDbContext(_options))
        {
            var repo = new BlocklistRepository(ctx);
            await repo.AddAsync(blocklist);
        }

        await using (var ctx = new StopMeDbContext(_options))
        {
            var repo = new BlocklistRepository(ctx);
            var roundtripped = await repo.GetByIdAsync(blocklist.Id);
            roundtripped.Should().NotBeNull();
            roundtripped!.Rules.Should().HaveCount(2);
            roundtripped.Rules.Select(r => r.Value).Should().BeEquivalentTo(new[] { "youtube.com", "*.tiktok.com" });
        }
    }

    [Fact]
    public async Task Session_with_blocklists_roundtrips()
    {
        var userId = Guid.NewGuid();
        var blocklistId = Guid.NewGuid();

        await using (var ctx = new StopMeDbContext(_options))
        {
            ctx.Blocklists.Add(new Blocklist
            {
                Id = blocklistId,
                UserProfileId = userId,
                Name = "Work Hours",
                Mode = BlocklistMode.Blacklist,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
            });

            ctx.FocusSessions.Add(new FocusSession
            {
                Id = Guid.NewGuid(),
                UserProfileId = userId,
                Name = "Morning Focus",
                StartAtUtc = new DateTime(2026, 6, 8, 8, 0, 0, DateTimeKind.Utc),
                EndAtUtc = new DateTime(2026, 6, 8, 12, 0, 0, DateTimeKind.Utc),
                Status = SessionStatus.Scheduled,
                Mode = SessionMode.Strict,
                LockPolicy = LockPolicy.TimeLocked,
                TimeZoneId = "Europe/Paris",
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                SessionBlocklists =
                {
                    new SessionBlocklist { BlocklistId = blocklistId },
                },
            });

            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new StopMeDbContext(_options))
        {
            var session = await ctx.FocusSessions
                .Include(s => s.SessionBlocklists)
                .FirstAsync();
            session.SessionBlocklists.Should().ContainSingle();
            session.SessionBlocklists.Single().BlocklistId.Should().Be(blocklistId);
            session.Mode.Should().Be(SessionMode.Strict);
        }
    }

    [Fact]
    public async Task Category_seeder_imports_default_categories()
    {
        await using var ctx = new StopMeDbContext(_options);
        var seeder = new CategorySeeder(ctx);

        var added = await seeder.SeedAsync();
        added.Should().BeGreaterThan(50);

        var socialDomains = await new CategoryRepository(ctx).GetDomainsAsync("Social Media");
        socialDomains.Should().Contain("tiktok.com");
        socialDomains.Should().Contain("instagram.com");
    }

    [Fact]
    public async Task Category_seeder_is_idempotent()
    {
        await using var ctx = new StopMeDbContext(_options);
        var seeder = new CategorySeeder(ctx);

        var first = await seeder.SeedAsync();
        var second = await seeder.SeedAsync();

        first.Should().BeGreaterThan(0);
        second.Should().Be(0);
    }

    public void Dispose()
    {
        _conn.Dispose();
        GC.SuppressFinalize(this);
    }
}
