using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Core.ValueObjects;
using StopMe.Infrastructure.Persistence;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.Ipc;
using StopMe.Service.Ipc.Handlers;
using StopMe.Service.State;
using StopMe.Service.Tests.Fakes;
using Xunit;

namespace StopMe.Service.Tests.Ipc;

public class HandlerTests
{
    [Fact]
    public async Task Status_returns_snapshot_counts()
    {
        await using var host = new TestHost();
        var state = host.Services.GetRequiredService<ServiceState>();
        state.UpdateSnapshot(new RuleSnapshot(
            ComputedAtUtc: host.Clock.GetUtcNow().UtcDateTime,
            ActiveSessions: new[]
            {
                new ActiveSessionInstance
                {
                    SessionId = Guid.NewGuid(),
                    Name = "Test",
                    StartAtUtc = DateTime.UtcNow,
                    EndAtUtc = DateTime.UtcNow.AddHours(1),
                    Mode = SessionMode.Soft,
                    LockPolicy = LockPolicy.None,
                    BlocklistIds = Array.Empty<Guid>(),
                },
            },
            CompiledRules: Array.Empty<CompiledRule>()),
            host.Clock.GetUtcNow().UtcDateTime);
        state.MarkBootRestoreCompleted();

        var resp = await DispatchAsync(host, Operations.Status);
        resp.Success.Should().BeTrue();
        var dto = resp.PayloadAs<StatusDto>()!;
        dto.ActiveSessionCount.Should().Be(1);
        dto.BootRestoreCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task EvaluateUrl_uses_current_snapshot()
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

        var resp = await DispatchAsync(host, Operations.EvaluateUrl,
            new EvaluateUrlRequest { Url = "https://youtube.com/watch" });
        resp.Success.Should().BeTrue();
        var dto = resp.PayloadAs<EvaluateUrlResponse>()!;
        dto.Blocked.Should().BeTrue();
        dto.MatchedRuleType.Should().Be(nameof(RuleType.Domain));
    }

    [Fact]
    public async Task EvaluateApp_uses_current_snapshot()
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
                    Type = RuleType.WindowsProcessName,
                    Value = "steam.exe",
                    SourceMode = BlocklistMode.Blacklist,
                },
            }),
            host.Clock.GetUtcNow().UtcDateTime);

        var resp = await DispatchAsync(host, Operations.EvaluateApp,
            new EvaluateAppRequest { ProcessId = 100, ProcessName = "steam" });
        resp.Success.Should().BeTrue();
        var dto = resp.PayloadAs<EvaluateAppResponse>()!;
        dto.Blocked.Should().BeTrue();
    }

    [Fact]
    public async Task Create_then_list_blocklists_roundtrips()
    {
        await using var host = new TestHost();

        var createResp = await DispatchAsync(host, Operations.CreateBlocklist,
            new CreateBlocklistRequest
            {
                Name = "Social",
                Mode = nameof(BlocklistMode.Blacklist),
                Rules = new[]
                {
                    new CreateRuleRequest { Type = nameof(RuleType.Domain), Value = "tiktok.com" },
                    new CreateRuleRequest { Type = nameof(RuleType.Wildcard), Value = "*.youtube.com" },
                },
            });
        createResp.Success.Should().BeTrue();
        var created = createResp.PayloadAs<BlocklistDto>()!;
        created.Rules.Should().HaveCount(2);

        var listResp = await DispatchAsync(host, Operations.ListBlocklists);
        listResp.Success.Should().BeTrue();
        var list = listResp.PayloadAs<BlocklistDto[]>()!;
        list.Should().ContainSingle().Which.Id.Should().Be(created.Id);
    }

    [Fact]
    public async Task Delete_removes_blocklist()
    {
        await using var host = new TestHost();
        var createResp = await DispatchAsync(host, Operations.CreateBlocklist,
            new CreateBlocklistRequest { Name = "Doomed", Mode = nameof(BlocklistMode.Blacklist) });
        var id = createResp.PayloadAs<BlocklistDto>()!.Id;

        var delResp = await DispatchAsync(host, Operations.DeleteBlocklist,
            new DeleteBlocklistRequest { Id = id });
        delResp.Success.Should().BeTrue();

        var listResp = await DispatchAsync(host, Operations.ListBlocklists);
        listResp.PayloadAs<BlocklistDto[]>()!.Should().BeEmpty();
    }

    [Fact]
    public async Task StartSession_marks_active()
    {
        await using var host = new TestHost();
        var nowUtc = host.Clock.GetUtcNow().UtcDateTime;
        var sessionId = Guid.NewGuid();

        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.FocusSessions.Add(new FocusSession
            {
                Id = sessionId,
                UserProfileId = DefaultUserContext.DefaultId,
                Name = "Spec",
                StartAtUtc = nowUtc.AddMinutes(-10),
                EndAtUtc = nowUtc.AddMinutes(30),
                Status = SessionStatus.Scheduled,
                Mode = SessionMode.Soft,
                LockPolicy = LockPolicy.None,
                TimeZoneId = "UTC",
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
            });
            await db.SaveChangesAsync();
        }

        var resp = await DispatchAsync(host, Operations.StartSession,
            new StartSessionRequest { SessionId = sessionId });
        resp.Success.Should().BeTrue();

        await using var s = host.CreateScope();
        var db2 = s.ServiceProvider.GetRequiredService<StopMeDbContext>();
        (await db2.FocusSessions.FindAsync(sessionId))!.Status.Should().Be(SessionStatus.Active);
    }

    [Fact]
    public async Task StopSession_strict_returns_denial()
    {
        await using var host = new TestHost();
        var nowUtc = host.Clock.GetUtcNow().UtcDateTime;
        var sessionId = Guid.NewGuid();

        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.FocusSessions.Add(new FocusSession
            {
                Id = sessionId,
                UserProfileId = DefaultUserContext.DefaultId,
                Name = "Strict",
                StartAtUtc = nowUtc.AddMinutes(-10),
                EndAtUtc = nowUtc.AddMinutes(30),
                Status = SessionStatus.Active,
                Mode = SessionMode.Strict,
                LockPolicy = LockPolicy.TimeLocked,
                TimeZoneId = "UTC",
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
            });
            await db.SaveChangesAsync();
        }

        var resp = await DispatchAsync(host, Operations.StopSession, new StopSessionRequest
        {
            SessionId = sessionId,
            Reason = nameof(StopReason.UserRequested),
            UserConfirmed = true,
        });

        resp.Success.Should().BeTrue();
        var dto = resp.PayloadAs<StopSessionResponse>()!;
        dto.Stopped.Should().BeFalse();
        dto.DenialReason.Should().NotBeNullOrEmpty();
    }

    private static Task<LocalApiResponse> DispatchAsync(TestHost host, string op) =>
        host.Services.GetRequiredService<LocalApiDispatcher>()
            .DispatchAsync(LocalApiRequest.For(op, host.Token), default);

    private static Task<LocalApiResponse> DispatchAsync<T>(TestHost host, string op, T payload) =>
        host.Services.GetRequiredService<LocalApiDispatcher>()
            .DispatchAsync(LocalApiRequest.For(op, payload, host.Token), default);
}
