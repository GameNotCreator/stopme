using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.ValueObjects;
using StopMe.Infrastructure.Persistence;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.Antibypass;
using StopMe.Service.Ipc;
using StopMe.Service.State;
using StopMe.Service.Tests.Fakes;
using Xunit;

namespace StopMe.Service.Tests.Ipc;

public class Phase5HandlerTests
{
    [Fact]
    public async Task ExtensionHeartbeat_records_into_health_monitor()
    {
        await using var host = new TestHost();
        var resp = await DispatchAsync(host, Operations.ExtensionHeartbeat,
            new ExtensionHeartbeatRequest { Browser = "chrome", ExtensionId = "abc", ExtensionVersion = "1.0" });
        resp.Success.Should().BeTrue();

        host.Services.GetRequiredService<IExtensionHealthMonitor>()
            .SnapshotAll()
            .Should().ContainSingle(s => s.Browser == "chrome" && s.ExtensionId == "abc");
    }

    [Fact]
    public async Task ExtensionHeartbeat_rejects_missing_fields()
    {
        await using var host = new TestHost();
        var resp = await DispatchAsync(host, Operations.ExtensionHeartbeat,
            new ExtensionHeartbeatRequest { Browser = "", ExtensionId = "" });
        resp.Success.Should().BeFalse();
        resp.ErrorCode.Should().Be(ErrorCodes.BadRequest);
    }

    [Fact]
    public async Task ExtensionHealth_returns_stale_flag_per_browser()
    {
        await using var host = new TestHost();
        var nowUtc = host.Clock.GetUtcNow().UtcDateTime;

        host.Services.GetRequiredService<IExtensionHealthMonitor>()
            .RecordHeartbeat(new ExtensionHeartbeat { Browser = "chrome", ExtensionId = "abc" }, nowUtc);
        host.Services.GetRequiredService<IExtensionHealthMonitor>()
            .RecordHeartbeat(new ExtensionHeartbeat { Browser = "edge",   ExtensionId = "def" }, nowUtc.AddHours(-1));

        var resp = await DispatchRawAsync(host, Operations.ExtensionHealth);
        resp.Success.Should().BeTrue();
        var dto = resp.PayloadAs<ExtensionHealthDto>()!;
        dto.Browsers.Should().HaveCount(2);
        dto.Browsers.First(b => b.Browser == "chrome").IsStale.Should().BeFalse();
        dto.Browsers.First(b => b.Browser == "edge").IsStale.Should().BeTrue();
    }

    [Fact]
    public async Task BypassEvents_returns_recent_records()
    {
        await using var host = new TestHost();
        var sessionId = Guid.NewGuid();
        var nowUtc = host.Clock.GetUtcNow().UtcDateTime;

        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.BypassEvents.Add(new BypassEvent
            {
                Type = BypassEventType.ClockDrift,
                DetectedAtUtc = nowUtc,
                SessionId = sessionId,
                Source = "Test",
                Detail = "boom",
            });
            await db.SaveChangesAsync();
        }

        var resp = await DispatchRawAsync(host, Operations.BypassEvents);
        resp.Success.Should().BeTrue();
        var dto = resp.PayloadAs<BypassEventsResponse>()!;
        dto.Events.Should().ContainSingle().Which.Type.Should().Be(nameof(BypassEventType.ClockDrift));
    }

    [Fact]
    public async Task RuleSnapshot_returns_current_compiled_rules()
    {
        await using var host = new TestHost();
        var state = host.Services.GetRequiredService<ServiceState>();
        state.UpdateSnapshot(new RuleSnapshot(
            host.Clock.GetUtcNow().UtcDateTime,
            Array.Empty<ActiveSessionInstance>(),
            new[]
            {
                new CompiledRule
                {
                    RuleId = Guid.NewGuid(),
                    BlocklistId = Guid.NewGuid(),
                    Type = RuleType.Domain,
                    Value = "youtube.com",
                    SourceMode = BlocklistMode.Blacklist,
                },
            }),
            host.Clock.GetUtcNow().UtcDateTime);

        var resp = await DispatchRawAsync(host, Operations.RuleSnapshot);
        resp.Success.Should().BeTrue();
        var dto = resp.PayloadAs<RuleSnapshotResponse>()!;
        dto.Rules.Should().ContainSingle().Which.Value.Should().Be("youtube.com");
    }

    [Fact]
    public async Task DeleteBlocklist_denied_during_active_session_records_bypass_event()
    {
        await using var host = new TestHost();
        var blocklistId = Guid.NewGuid();
        var nowUtc = host.Clock.GetUtcNow().UtcDateTime;

        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.Blocklists.Add(new Blocklist
            {
                Id = blocklistId,
                UserProfileId = DefaultUserContext.DefaultId,
                Name = "Active",
                Mode = BlocklistMode.Blacklist,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
            });
            await db.SaveChangesAsync();
        }

        var state = host.Services.GetRequiredService<ServiceState>();
        state.UpdateSnapshot(new RuleSnapshot(
            nowUtc,
            new[]
            {
                new ActiveSessionInstance
                {
                    SessionId = Guid.NewGuid(),
                    Name = "Focus",
                    StartAtUtc = nowUtc,
                    EndAtUtc = nowUtc.AddHours(1),
                    Mode = SessionMode.Soft,
                    LockPolicy = LockPolicy.None,
                    BlocklistIds = new[] { blocklistId },
                },
            },
            Array.Empty<CompiledRule>()),
            nowUtc);

        var resp = await DispatchAsync(host, Operations.DeleteBlocklist,
            new DeleteBlocklistRequest { Id = blocklistId });

        resp.Success.Should().BeFalse();
        resp.ErrorCode.Should().Be(ErrorCodes.Conflict);

        await using var s = host.CreateScope();
        var db2 = s.ServiceProvider.GetRequiredService<StopMeDbContext>();
        (await db2.BypassEvents.CountAsync(e => e.Type == BypassEventType.BlocklistDeleteAttempted))
            .Should().Be(1);
    }

    [Fact]
    public async Task UpdateBlocklist_denied_during_strict_session_records_bypass_event()
    {
        await using var host = new TestHost();
        var blocklistId = Guid.NewGuid();
        var nowUtc = host.Clock.GetUtcNow().UtcDateTime;

        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.Blocklists.Add(new Blocklist
            {
                Id = blocklistId,
                UserProfileId = DefaultUserContext.DefaultId,
                Name = "Locked",
                Mode = BlocklistMode.Blacklist,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
            });
            await db.SaveChangesAsync();
        }

        var state = host.Services.GetRequiredService<ServiceState>();
        state.UpdateSnapshot(new RuleSnapshot(
            nowUtc,
            new[]
            {
                new ActiveSessionInstance
                {
                    SessionId = Guid.NewGuid(),
                    Name = "Strict",
                    StartAtUtc = nowUtc,
                    EndAtUtc = nowUtc.AddHours(1),
                    Mode = SessionMode.Strict,
                    LockPolicy = LockPolicy.TimeLocked,
                    BlocklistIds = new[] { blocklistId },
                },
            },
            Array.Empty<CompiledRule>()),
            nowUtc);

        var resp = await DispatchAsync(host, Operations.UpdateBlocklist,
            new UpdateBlocklistRequest { Id = blocklistId, Name = "renamed" });

        resp.Success.Should().BeFalse();
        resp.ErrorCode.Should().Be(ErrorCodes.Conflict);

        await using var s = host.CreateScope();
        var db2 = s.ServiceProvider.GetRequiredService<StopMeDbContext>();
        (await db2.BypassEvents.CountAsync(e => e.Type == BypassEventType.BlocklistEditAttempted))
            .Should().Be(1);
    }

    private static Task<LocalApiResponse> DispatchRawAsync(TestHost host, string op) =>
        host.Services.GetRequiredService<LocalApiDispatcher>()
            .DispatchAsync(LocalApiRequest.For(op, host.Token), default);

    private static Task<LocalApiResponse> DispatchAsync<T>(TestHost host, string op, T payload) =>
        host.Services.GetRequiredService<LocalApiDispatcher>()
            .DispatchAsync(LocalApiRequest.For(op, payload, host.Token), default);
}
