using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StopMe.Core.Enums;
using StopMe.Core.ValueObjects;
using StopMe.Infrastructure.Persistence;
using StopMe.Service.Antibypass;
using StopMe.Service.Tests.Fakes;
using Xunit;

namespace StopMe.Service.Tests.Antibypass;

public class AntiBypassRunnerTests
{
    [Fact]
    public async Task Does_nothing_when_no_strict_session_active()
    {
        await using var host = new TestHost();
        var runner = host.Services.GetRequiredService<AntiBypassRunner>();
        var nowUtc = host.Clock.GetUtcNow().UtcDateTime;

        // Stale heartbeat — but no strict session, so we should NOT emit a bypass event.
        host.Services.GetRequiredService<IExtensionHealthMonitor>()
            .RecordHeartbeat(new ExtensionHeartbeat { Browser = "chrome", ExtensionId = "x" }, nowUtc.AddHours(-1));

        await runner.RunAsync(nowUtc, Array.Empty<ActiveSessionInstance>(), default);

        await using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
        (await db.BypassEvents.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Stale_heartbeat_during_strict_session_emits_bypass_event()
    {
        await using var host = new TestHost();
        var runner = host.Services.GetRequiredService<AntiBypassRunner>();
        var nowUtc = host.Clock.GetUtcNow().UtcDateTime;

        host.Services.GetRequiredService<IExtensionHealthMonitor>()
            .RecordHeartbeat(new ExtensionHeartbeat { Browser = "chrome", ExtensionId = "x" }, nowUtc.AddHours(-1));

        var active = new[] { Strict("Lockdown") };
        await runner.RunAsync(nowUtc, active, default);

        await using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
        var events = await db.BypassEvents.ToListAsync();
        events.Should().ContainSingle().Which.Type.Should().Be(BypassEventType.ExtensionStale);
    }

    [Fact]
    public async Task Same_stale_browser_is_not_double_reported()
    {
        await using var host = new TestHost();
        var runner = host.Services.GetRequiredService<AntiBypassRunner>();
        var nowUtc = host.Clock.GetUtcNow().UtcDateTime;
        host.Services.GetRequiredService<IExtensionHealthMonitor>()
            .RecordHeartbeat(new ExtensionHeartbeat { Browser = "chrome", ExtensionId = "x" }, nowUtc.AddHours(-1));

        var active = new[] { Strict("Lockdown") };
        await runner.RunAsync(nowUtc, active, default);
        await runner.RunAsync(nowUtc.AddSeconds(2), active, default);

        await using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
        (await db.BypassEvents.CountAsync()).Should().Be(1);
    }

    private static ActiveSessionInstance Strict(string name) => new()
    {
        SessionId = Guid.NewGuid(),
        Name = name,
        StartAtUtc = DateTime.UtcNow,
        EndAtUtc = DateTime.UtcNow.AddHours(1),
        Mode = SessionMode.Strict,
        LockPolicy = LockPolicy.TimeLocked,
        BlocklistIds = Array.Empty<Guid>(),
    };
}
