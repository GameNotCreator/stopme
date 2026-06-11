using FluentAssertions;
using StopMe.App.Core.Tests.Fakes;
using StopMe.App.Core.ViewModels;
using StopMe.LocalApi.Dtos;
using Xunit;

namespace StopMe.App.Core.Tests.ViewModels;

public class ActiveSessionViewModelTests
{
    [Fact]
    public async Task Refresh_populates_sessions_and_focuses_first()
    {
        var fake = new FakeServiceConnection();
        fake.ActiveSessionsToReturn.Add(Session("A"));
        fake.ActiveSessionsToReturn.Add(Session("B"));

        var vm = new ActiveSessionViewModel(fake);
        await vm.RefreshAsync();

        vm.Sessions.Should().HaveCount(2);
        vm.Focused.Should().NotBeNull();
        vm.Focused!.Name.Should().Be("A");
    }

    [Fact]
    public async Task StopSession_sends_payload_and_removes_from_list_on_success()
    {
        var fake = new FakeServiceConnection
        {
            StopSessionResponseToReturn = new StopSessionResponse { Stopped = true },
        };
        fake.ActiveSessionsToReturn.Add(Session("Focus"));

        var vm = new ActiveSessionViewModel(fake);
        await vm.RefreshAsync();
        vm.TypedPhrase = "I choose to stop focusing";
        vm.UserConfirmed = true;

        await vm.StopSessionAsync();

        fake.StopRequests.Should().ContainSingle();
        fake.StopRequests[0].UserConfirmed.Should().BeTrue();
        fake.StopRequests[0].TypedText.Should().Be("I choose to stop focusing");
        vm.Sessions.Should().BeEmpty();
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task StopSession_sets_error_when_denied()
    {
        var fake = new FakeServiceConnection
        {
            StopSessionResponseToReturn = new StopSessionResponse { Stopped = false, DenialReason = "TimeLocked" },
        };
        fake.ActiveSessionsToReturn.Add(Session("Strict"));

        var vm = new ActiveSessionViewModel(fake);
        await vm.RefreshAsync();
        await vm.StopSessionAsync();

        vm.HasError.Should().BeTrue();
        vm.StatusMessage.Should().Be("TimeLocked");
        vm.Sessions.Should().ContainSingle();
    }

    private static ActiveSessionDto Session(string name) => new()
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
}
