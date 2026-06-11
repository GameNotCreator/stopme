using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StopMe.Core.Enums;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;
using StopMe.Service.Boot;
using StopMe.Service.Ipc;
using StopMe.Service.Tests.Fakes;
using Xunit;

namespace StopMe.Service.Tests.Boot;

public class BootRestorerTests
{
    [Fact]
    public async Task Restoration_marks_scheduled_session_in_window_as_active()
    {
        var nowUtc = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);
        await using var host = new TestHost(nowUtc);
        var sessionId = await AddOneShotAsync(host,
            start: nowUtc.AddMinutes(-30),
            end: nowUtc.AddMinutes(30),
            status: SessionStatus.Scheduled);

        var restorer = ActivatorUtilities.CreateInstance<BootRestorer>(host.Services);
        await restorer.RestoreAsync(CancellationToken.None);

        await using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
        (await db.FocusSessions.FindAsync(sessionId))!.Status.Should().Be(SessionStatus.Active);
    }

    [Fact]
    public async Task Restoration_completes_expired_session()
    {
        var nowUtc = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);
        await using var host = new TestHost(nowUtc);
        var sessionId = await AddOneShotAsync(host,
            start: nowUtc.AddHours(-3),
            end: nowUtc.AddHours(-1),
            status: SessionStatus.Active);

        var restorer = ActivatorUtilities.CreateInstance<BootRestorer>(host.Services);
        await restorer.RestoreAsync(CancellationToken.None);

        await using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
        (await db.FocusSessions.FindAsync(sessionId))!.Status.Should().Be(SessionStatus.Completed);
    }

    [Fact]
    public async Task Restoration_skips_recurring_sessions()
    {
        var nowUtc = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);
        await using var host = new TestHost(nowUtc);

        await using (var scope = host.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
            db.FocusSessions.Add(new FocusSession
            {
                Id = Guid.NewGuid(),
                UserProfileId = Guid.NewGuid(),
                Name = "Recurring",
                StartAtUtc = nowUtc.AddHours(-2),  // already in the past but recurring
                EndAtUtc = nowUtc.AddHours(-1),
                IsRecurring = true,
                RecurrenceRule = "FREQ=DAILY",
                Status = SessionStatus.Scheduled,
                Mode = SessionMode.Soft,
                LockPolicy = LockPolicy.None,
                TimeZoneId = "UTC",
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
            });
            await db.SaveChangesAsync();
        }

        var restorer = ActivatorUtilities.CreateInstance<BootRestorer>(host.Services);
        await restorer.RestoreAsync(CancellationToken.None);

        await using var s = host.CreateScope();
        var db2 = s.ServiceProvider.GetRequiredService<StopMeDbContext>();
        var session = await db2.FocusSessions.FirstAsync();
        session.Status.Should().NotBe(SessionStatus.Completed);
    }

    [Fact]
    public async Task Restoration_marks_state_completed()
    {
        await using var host = new TestHost();
        var restorer = ActivatorUtilities.CreateInstance<BootRestorer>(host.Services);
        await restorer.RestoreAsync(CancellationToken.None);

        host.Services.GetRequiredService<Service.State.ServiceState>()
            .BootRestoreCompleted.Should().BeTrue();
    }

    private static async Task<Guid> AddOneShotAsync(TestHost host, DateTime start, DateTime end, SessionStatus status)
    {
        var id = Guid.NewGuid();
        await using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
        db.FocusSessions.Add(new FocusSession
        {
            Id = id,
            UserProfileId = Guid.NewGuid(),
            Name = "Test",
            StartAtUtc = start,
            EndAtUtc = end,
            Status = status,
            Mode = SessionMode.Soft,
            LockPolicy = LockPolicy.None,
            IsRecurring = false,
            TimeZoneId = "UTC",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return id;
    }
}
