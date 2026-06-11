using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;
using StopMe.Infrastructure.Persistence;
using StopMe.Service.Processes;
using StopMe.Service.Tests.Fakes;
using Xunit;

namespace StopMe.Service.Tests.Processes;

public class ProcessWatcherTests
{
    [Fact]
    public async Task Skips_when_no_rules()
    {
        await using var host = new TestHost();
        host.ProcessProvider.Snapshot.Add(MakeApp(123, "steam"));

        var watcher = host.Services.GetRequiredService<ProcessWatcher>();
        var killed = await watcher.EnforceAsync(Array.Empty<CompiledRule>(), default);

        killed.Should().Be(0);
        host.ProcessTerminator.Terminations.Should().BeEmpty();
    }

    [Fact]
    public async Task Terminates_blocked_process_and_records_event()
    {
        await using var host = new TestHost();
        var sessionId = Guid.NewGuid();
        var rules = new[]
        {
            new CompiledRule
            {
                RuleId = Guid.NewGuid(),
                BlocklistId = Guid.NewGuid(),
                Type = RuleType.WindowsProcessName,
                Value = "steam.exe",
                SourceMode = BlocklistMode.Blacklist,
                SessionId = sessionId,
                SessionName = "Work Hours",
                SessionEndUtc = DateTime.UtcNow.AddHours(1),
            },
        };
        host.ProcessProvider.Snapshot.Add(MakeApp(123, "steam"));
        host.ProcessProvider.Snapshot.Add(MakeApp(456, "notepad"));

        var watcher = host.Services.GetRequiredService<ProcessWatcher>();
        var killed = await watcher.EnforceAsync(rules, default);

        killed.Should().Be(1);
        host.ProcessTerminator.Terminations.Should().ContainSingle()
            .Which.Pid.Should().Be(123);
        host.Notifications.AppBlocked.Should().ContainSingle();

        await using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
        (await db.UsageEvents.CountAsync(e => e.Type == UsageEventType.AppBlocked && e.SessionId == sessionId))
            .Should().Be(1);
    }

    [Fact]
    public async Task AccessDenied_does_not_record_event()
    {
        await using var host = new TestHost();
        host.ProcessTerminator.NextResult = TerminationResult.AccessDenied;
        host.ProcessProvider.Snapshot.Add(MakeApp(999, "protected"));

        var rules = new[]
        {
            new CompiledRule
            {
                RuleId = Guid.NewGuid(),
                BlocklistId = Guid.NewGuid(),
                Type = RuleType.WindowsProcessName,
                Value = "protected.exe",
                SourceMode = BlocklistMode.Blacklist,
            },
        };

        var watcher = host.Services.GetRequiredService<ProcessWatcher>();
        var killed = await watcher.EnforceAsync(rules, default);

        killed.Should().Be(0);
        host.Notifications.AppBlocked.Should().BeEmpty();
    }

    private static RunningAppInfo MakeApp(int pid, string name) =>
        new() { ProcessId = pid, ProcessName = name };
}
