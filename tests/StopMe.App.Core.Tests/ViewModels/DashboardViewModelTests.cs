using FluentAssertions;
using StopMe.App.Core.Tests.Fakes;
using StopMe.App.Core.ViewModels;
using StopMe.LocalApi.Dtos;
using Xunit;

namespace StopMe.App.Core.Tests.ViewModels;

public class DashboardViewModelTests
{
    [Fact]
    public async Task Refresh_populates_state_from_service()
    {
        var fake = new FakeServiceConnection
        {
            StatusToReturn = new StatusDto
            {
                ServiceVersion = "0.1.0",
                NowUtc = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc),
                LastTickUtc = new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc),
                BootRestoreCompleted = true,
                ActiveSessionCount = 1,
                CompiledRuleCount = 12,
            },
        };
        fake.ActiveSessionsToReturn.Add(MakeSession("Focus"));
        fake.BlocklistsToReturn.Add(MakeBlocklist("Social Media"));

        var vm = new DashboardViewModel(fake);

        await vm.RefreshAsync();

        vm.ServiceVersion.Should().Be("0.1.0");
        vm.BootRestoreCompleted.Should().BeTrue();
        vm.ActiveSessions.Should().ContainSingle();
        vm.BlocklistCount.Should().Be(1);
        vm.HasError.Should().BeFalse();
        vm.HasNoActiveSession.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_sets_error_when_service_unreachable()
    {
        var fake = new FakeServiceConnection { LastError = "service down" };

        var vm = new DashboardViewModel(fake);
        await vm.RefreshAsync();

        vm.HasError.Should().BeTrue();
        vm.StatusMessage.Should().Be("service down");
    }

    [Fact]
    public async Task Refresh_pulls_recent_bypass_events()
    {
        var fake = new FakeServiceConnection
        {
            StatusToReturn = new StatusDto
            {
                ServiceVersion = "0.1.0",
                NowUtc = DateTime.UtcNow,
                LastTickUtc = DateTime.UtcNow,
                BootRestoreCompleted = true,
                ActiveSessionCount = 0,
                CompiledRuleCount = 0,
            },
        };
        fake.BypassEventsToReturn.Add(new BypassEventDto
        {
            Id = Guid.NewGuid(),
            Type = "ClockDrift",
            DetectedAtUtc = DateTime.UtcNow,
            Detail = "drift detected",
        });

        var vm = new DashboardViewModel(fake);
        await vm.RefreshAsync();

        vm.RecentBypassEvents.Should().ContainSingle();
        vm.HasNoBypassEvents.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_replaces_existing_session_collection()
    {
        var fake = new FakeServiceConnection
        {
            StatusToReturn = new StatusDto
            {
                ServiceVersion = "0.1.0",
                NowUtc = DateTime.UtcNow,
                LastTickUtc = DateTime.UtcNow,
                BootRestoreCompleted = true,
                ActiveSessionCount = 1,
                CompiledRuleCount = 0,
            },
        };
        fake.ActiveSessionsToReturn.Add(MakeSession("First"));
        var vm = new DashboardViewModel(fake);
        await vm.RefreshAsync();
        vm.ActiveSessions.Should().ContainSingle();

        fake.ActiveSessionsToReturn.Clear();
        fake.ActiveSessionsToReturn.Add(MakeSession("Second"));
        await vm.RefreshAsync();

        vm.ActiveSessions.Should().ContainSingle()
            .Which.Name.Should().Be("Second");
    }

    private static ActiveSessionDto MakeSession(string name) => new()
    {
        SessionId = Guid.NewGuid(),
        Name = name,
        StartAtUtc = DateTime.UtcNow,
        EndAtUtc = DateTime.UtcNow.AddHours(1),
        Mode = "Soft",
        LockPolicy = "None",
        RemainingSeconds = 3600,
        BlocklistIds = Array.Empty<Guid>(),
    };

    private static BlocklistDto MakeBlocklist(string name) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Mode = "Blacklist",
        IsEnabled = true,
        Rules = Array.Empty<BlockRuleDto>(),
    };
}
