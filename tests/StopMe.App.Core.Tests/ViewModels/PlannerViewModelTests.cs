using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using StopMe.App.Core.Tests.Fakes;
using StopMe.App.Core.ViewModels;
using StopMe.LocalApi.Dtos;
using Xunit;

namespace StopMe.App.Core.Tests.ViewModels;

public class PlannerViewModelTests
{
    private readonly FakeServiceConnection _fake = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 10, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Refresh_loads_blocklists_with_existing_selection_preserved()
    {
        _fake.BlocklistsToReturn.Add(new BlocklistDto
        {
            Id = Guid.NewGuid(),
            Name = "Social",
            Mode = "Blacklist",
            IsEnabled = true,
            Rules = Array.Empty<BlockRuleDto>(),
        });

        var vm = NewViewModel();
        await vm.RefreshAsync();
        vm.Blocklists.Should().ContainSingle();

        vm.Blocklists[0].IsSelected = true;
        await vm.RefreshAsync();
        vm.Blocklists[0].IsSelected.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAndStart_requires_a_blocklist()
    {
        var vm = NewViewModel();
        await vm.RefreshAsync();
        await vm.CreateAndStartAsync();
        vm.HasError.Should().BeTrue();
        _fake.CreateSessionRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAndStart_sends_request_with_picked_values()
    {
        var blocklistId = Guid.NewGuid();
        _fake.BlocklistsToReturn.Add(new BlocklistDto
        {
            Id = blocklistId, Name = "Distractions", Mode = "Blacklist",
            IsEnabled = true, Rules = Array.Empty<BlockRuleDto>(),
        });
        _fake.CreateSessionResponseToReturn = new CreateSessionResponse
        {
            SessionId = Guid.NewGuid(), Started = true,
        };

        var vm = NewViewModel();
        await vm.RefreshAsync();
        vm.Blocklists[0].IsSelected = true;
        vm.SessionName = "Deep work";
        vm.DurationMinutes = 90;
        vm.SelectedMode = "Strict";
        vm.SelectedLockPolicy = "TimeLocked";

        await vm.CreateAndStartAsync();

        var sent = _fake.CreateSessionRequests.Single();
        sent.Name.Should().Be("Deep work");
        sent.Mode.Should().Be("Strict");
        sent.LockPolicy.Should().Be("TimeLocked");
        sent.BlocklistIds.Should().BeEquivalentTo(new[] { blocklistId });
        (sent.EndAtUtc - sent.StartAtUtc).TotalMinutes.Should().Be(90);
        vm.HasError.Should().BeFalse();
        vm.LastCreatedSummary.Should().Contain("Deep work");
    }

    [Fact]
    public async Task RandomText_lock_requires_phrase()
    {
        _fake.BlocklistsToReturn.Add(new BlocklistDto
        {
            Id = Guid.NewGuid(), Name = "n", Mode = "Blacklist",
            IsEnabled = true, Rules = Array.Empty<BlockRuleDto>(),
        });
        var vm = NewViewModel();
        await vm.RefreshAsync();
        vm.Blocklists[0].IsSelected = true;
        vm.SelectedLockPolicy = "RandomText";
        vm.RequiredText = string.Empty;

        await vm.CreateAndStartAsync();
        vm.HasError.Should().BeTrue();
        _fake.CreateSessionRequests.Should().BeEmpty();
    }

    [Fact]
    public async Task Scheduled_start_converts_local_to_utc()
    {
        var blocklistId = Guid.NewGuid();
        _fake.BlocklistsToReturn.Add(new BlocklistDto
        {
            Id = blocklistId, Name = "n", Mode = "Blacklist",
            IsEnabled = true, Rules = Array.Empty<BlockRuleDto>(),
        });
        _fake.CreateSessionResponseToReturn = new CreateSessionResponse
        {
            SessionId = Guid.NewGuid(), Started = false,
        };

        var vm = NewViewModel();
        await vm.RefreshAsync();
        vm.Blocklists[0].IsSelected = true;
        vm.UseScheduledStart = true;
        var localDate = new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.FromHours(2));
        var localTime = new TimeSpan(14, 30, 0);
        vm.ScheduledStartDate = localDate;
        vm.ScheduledStartTime = localTime;

        await vm.CreateOnlyAsync();

        var sent = _fake.CreateSessionRequests.Single();
        var expectedLocal = DateTime.SpecifyKind(localDate.LocalDateTime.Date.Add(localTime), DateTimeKind.Local);
        sent.StartAtUtc.Should().Be(expectedLocal.ToUniversalTime());
        sent.StartImmediately.Should().BeFalse(because: "scheduled-for-later sessions never start immediately");
    }

    private PlannerViewModel NewViewModel() => new(_fake, _clock);
}
