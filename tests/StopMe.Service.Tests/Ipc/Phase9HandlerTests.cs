using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;
using StopMe.LocalApi;
using StopMe.LocalApi.Dtos;
using StopMe.Service.Ipc;
using StopMe.Service.Tests.Fakes;
using Xunit;

namespace StopMe.Service.Tests.Ipc;

// Covers the new IPC ops shipped for the Block-Apps flow:
//   processes.snapshot, sessions.create, blocklists.add_rule
public class Phase9HandlerTests
{
    [Fact]
    public async Task ProcessSnapshot_returns_apps_grouped_by_name()
    {
        await using var host = new TestHost();
        host.ProcessProvider.Snapshot.Clear();
        host.ProcessProvider.Snapshot.Add(new global::StopMe.Core.ValueObjects.RunningAppInfo
        {
            ProcessId = 1, ProcessName = "discord",
            ExecutablePath = "C:\\Discord.exe", MainWindowTitle = "Discord",
        });
        host.ProcessProvider.Snapshot.Add(new global::StopMe.Core.ValueObjects.RunningAppInfo
        {
            ProcessId = 2, ProcessName = "discord",
            ExecutablePath = "C:\\Discord.exe", MainWindowTitle = null,
        });

        var resp = await DispatchRawAsync(host, Operations.ProcessSnapshot);
        resp.Success.Should().BeTrue();
        var dto = resp.PayloadAs<ProcessSnapshotResponse>()!;
        dto.Apps.Should().ContainSingle();
        dto.Apps[0].ProcessName.Should().Be("discord");
        dto.Apps[0].HasVisibleWindow.Should().BeTrue();
    }

    [Fact]
    public async Task CreateSession_persists_session_with_blocklist_links()
    {
        await using var host = new TestHost();
        var blocklistId = Guid.NewGuid();
        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.Blocklists.Add(new Blocklist
            {
                Id = blocklistId,
                UserProfileId = DefaultUserContext.DefaultId,
                Name = "Quick",
                Mode = BlocklistMode.Blacklist,
                CreatedAtUtc = host.Clock.GetUtcNow().UtcDateTime,
                UpdatedAtUtc = host.Clock.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync();
        }

        var nowUtc = host.Clock.GetUtcNow().UtcDateTime;
        var resp = await DispatchAsync(host, Operations.CreateSession, new CreateSessionRequest
        {
            Name = "Quick block",
            StartAtUtc = nowUtc,
            EndAtUtc = nowUtc.AddMinutes(30),
            Mode = nameof(SessionMode.Soft),
            LockPolicy = nameof(LockPolicy.Confirmation),
            BlocklistIds = new[] { blocklistId },
            StartImmediately = true,
        });

        resp.Success.Should().BeTrue();
        var dto = resp.PayloadAs<CreateSessionResponse>()!;
        dto.SessionId.Should().NotBeEmpty();
        dto.Started.Should().BeTrue();

        await using var s = host.CreateScope();
        var db2 = s.ServiceProvider.GetRequiredService<StopMeDbContext>();
        var created = await db2.FocusSessions.Include(f => f.SessionBlocklists).FirstAsync(f => f.Id == dto.SessionId);
        created.Status.Should().Be(SessionStatus.Active);
        created.SessionBlocklists.Should().ContainSingle()
            .Which.BlocklistId.Should().Be(blocklistId);
    }

    [Fact]
    public async Task CreateSession_rejects_inverted_times()
    {
        await using var host = new TestHost();
        var nowUtc = host.Clock.GetUtcNow().UtcDateTime;
        var resp = await DispatchAsync(host, Operations.CreateSession, new CreateSessionRequest
        {
            Name = "Bad",
            StartAtUtc = nowUtc,
            EndAtUtc = nowUtc,
            Mode = nameof(SessionMode.Soft),
            LockPolicy = nameof(LockPolicy.None),
        });
        resp.Success.Should().BeFalse();
        resp.ErrorCode.Should().Be(ErrorCodes.BadRequest);
    }

    [Fact]
    public async Task AddRule_appends_rule_to_existing_blocklist()
    {
        await using var host = new TestHost();
        var blocklistId = Guid.NewGuid();
        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.Blocklists.Add(new Blocklist
            {
                Id = blocklistId,
                UserProfileId = DefaultUserContext.DefaultId,
                Name = "Apps",
                Mode = BlocklistMode.Blacklist,
                CreatedAtUtc = host.Clock.GetUtcNow().UtcDateTime,
                UpdatedAtUtc = host.Clock.GetUtcNow().UtcDateTime,
            });
            await db.SaveChangesAsync();
        }

        var resp = await DispatchAsync(host, Operations.AddRule, new AddRuleRequest
        {
            BlocklistId = blocklistId,
            Rules = new[]
            {
                new CreateRuleRequest { Type = nameof(RuleType.WindowsProcessName), Value = "steam.exe" },
                new CreateRuleRequest { Type = nameof(RuleType.WindowsProcessName), Value = "discord.exe" },
            },
        });

        resp.Success.Should().BeTrue($"got {resp.ErrorCode}: {resp.ErrorMessage}");
        var dto = resp.PayloadAs<AddRuleResponse>()!;
        dto.AddedCount.Should().Be(2);

        await using var s = host.CreateScope();
        var db2 = s.ServiceProvider.GetRequiredService<StopMeDbContext>();
        var blocklist = await db2.Blocklists.Include(b => b.Rules).FirstAsync(b => b.Id == blocklistId);
        blocklist.Rules.Select(r => r.Value)
            .Should().BeEquivalentTo(new[] { "steam.exe", "discord.exe" });
    }

    private static Task<LocalApiResponse> DispatchRawAsync(TestHost host, string op) =>
        host.Services.GetRequiredService<LocalApiDispatcher>()
            .DispatchAsync(LocalApiRequest.For(op, host.Token), default);

    private static Task<LocalApiResponse> DispatchAsync<T>(TestHost host, string op, T payload) =>
        host.Services.GetRequiredService<LocalApiDispatcher>()
            .DispatchAsync(LocalApiRequest.For(op, payload, host.Token), default);
}
