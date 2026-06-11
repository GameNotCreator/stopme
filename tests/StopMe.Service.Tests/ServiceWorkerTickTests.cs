using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;
using StopMe.Service.Boot;
using StopMe.Service.Ipc;
using StopMe.Service.Notifications;
using StopMe.Service.Processes;
using StopMe.Service.State;
using StopMe.Service.Tests.Fakes;
using Xunit;

namespace StopMe.Service.Tests;

public class ServiceWorkerTickTests
{
    [Fact]
    public async Task Tick_publishes_active_sessions_to_state()
    {
        var nowUtc = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);
        await using var host = new TestHost(nowUtc);

        var blocklistId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.Blocklists.Add(new Blocklist
            {
                Id = blocklistId,
                UserProfileId = DefaultUserContext.DefaultId,
                Name = "Social",
                Mode = BlocklistMode.Blacklist,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                Rules =
                {
                    new BlockRule { Id = Guid.NewGuid(), Type = RuleType.Domain, Value = "tiktok.com", CreatedAtUtc = nowUtc },
                },
            });
            db.FocusSessions.Add(new FocusSession
            {
                Id = sessionId,
                UserProfileId = DefaultUserContext.DefaultId,
                Name = "Now",
                StartAtUtc = nowUtc.AddMinutes(-5),
                EndAtUtc = nowUtc.AddMinutes(30),
                Status = SessionStatus.Active,
                Mode = SessionMode.Soft,
                LockPolicy = LockPolicy.None,
                TimeZoneId = "UTC",
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                SessionBlocklists = { new SessionBlocklist { BlocklistId = blocklistId } },
            });
            await db.SaveChangesAsync();
        }

        var worker = new ServiceWorker(
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            host.Services.GetRequiredService<ProcessWatcher>(),
            host.Services.GetRequiredService<INotificationSink>(),
            host.Services.GetRequiredService<BootRestorer>(),
            host.Services.GetRequiredService<Service.Antibypass.AntiBypassRunner>(),
            host.Services.GetRequiredService<ServiceState>(),
            host.Clock,
            host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<ServiceOptions>>(),
            NullLogger<ServiceWorker>.Instance);

        await worker.TickAsync(default);

        var state = host.Services.GetRequiredService<ServiceState>();
        state.Current.ActiveSessions.Should().ContainSingle()
            .Which.SessionId.Should().Be(sessionId);
        state.Current.CompiledRules.Should().ContainSingle()
            .Which.Value.Should().Be("tiktok.com");
    }

    [Fact]
    public async Task Tick_drives_process_watcher_to_terminate_matching_processes()
    {
        var nowUtc = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);
        await using var host = new TestHost(nowUtc);

        var blocklistId = Guid.NewGuid();

        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.Blocklists.Add(new Blocklist
            {
                Id = blocklistId,
                UserProfileId = DefaultUserContext.DefaultId,
                Name = "Gaming",
                Mode = BlocklistMode.Blacklist,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                Rules =
                {
                    new BlockRule { Id = Guid.NewGuid(), Type = RuleType.WindowsProcessName, Value = "steam.exe", CreatedAtUtc = nowUtc },
                },
            });
            db.FocusSessions.Add(new FocusSession
            {
                Id = Guid.NewGuid(),
                UserProfileId = DefaultUserContext.DefaultId,
                Name = "Now",
                StartAtUtc = nowUtc.AddMinutes(-5),
                EndAtUtc = nowUtc.AddMinutes(30),
                Status = SessionStatus.Active,
                Mode = SessionMode.Soft,
                LockPolicy = LockPolicy.None,
                TimeZoneId = "UTC",
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                SessionBlocklists = { new SessionBlocklist { BlocklistId = blocklistId } },
            });
            await db.SaveChangesAsync();
        }

        host.ProcessProvider.Snapshot.Add(new Core.ValueObjects.RunningAppInfo
        {
            ProcessId = 4242,
            ProcessName = "steam",
        });

        var worker = new ServiceWorker(
            host.Services.GetRequiredService<IServiceScopeFactory>(),
            host.Services.GetRequiredService<ProcessWatcher>(),
            host.Services.GetRequiredService<INotificationSink>(),
            host.Services.GetRequiredService<BootRestorer>(),
            host.Services.GetRequiredService<Service.Antibypass.AntiBypassRunner>(),
            host.Services.GetRequiredService<ServiceState>(),
            host.Clock,
            host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<ServiceOptions>>(),
            NullLogger<ServiceWorker>.Instance);

        await worker.TickAsync(default);

        host.ProcessTerminator.Terminations.Should().ContainSingle()
            .Which.Pid.Should().Be(4242);
    }
}
