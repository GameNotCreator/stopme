using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using StopMe.App.Core.Tests.Fakes;
using StopMe.App.Core.ViewModels;
using StopMe.LocalApi.Dtos;
using Xunit;

namespace StopMe.App.Core.Tests.ViewModels;

public class BlockAppsViewModelTests
{
    private readonly FakeServiceConnection _fake = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 6, 10, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Refresh_lists_running_apps_and_preserves_selection()
    {
        _fake.RunningAppsToReturn.AddRange(new[]
        {
            new RunningAppInfoDto { ProcessId = 1, ProcessName = "discord", MainWindowTitle = "Discord", HasVisibleWindow = true },
            new RunningAppInfoDto { ProcessId = 2, ProcessName = "steam",   HasVisibleWindow = false },
        });

        var vm = NewViewModel();
        await vm.RefreshAsync();

        vm.RunningApps.Should().HaveCount(2);
        vm.RunningApps[0].ProcessName.Should().Be("discord");

        // Select Discord, refresh again — selection survives.
        vm.RunningApps[0].IsSelected = true;
        vm.OnPropertyChangedSelectionStub(); // simulate the OnPropertyChanged via direct method
        await vm.RefreshAsync();
        vm.RunningApps.First(a => a.ProcessName == "discord").IsSelected.Should().BeTrue();
    }

    [Fact]
    public void AddManualEntry_normalises_and_dedupes()
    {
        var vm = NewViewModel();

        vm.ManualProcessName = "discord";
        vm.AddManualEntryCommand.Execute(null);
        vm.ManualProcessName.Should().BeEmpty();
        vm.ManualEntries.Single().ProcessName.Should().Be("discord.exe");

        vm.ManualProcessName = "DISCORD.EXE";
        vm.AddManualEntryCommand.Execute(null);
        vm.ManualEntries.Should().HaveCount(1, because: "duplicates are dropped case-insensitively");
    }

    [Fact]
    public async Task StartBlocking_creates_blocklist_and_session_with_process_rules()
    {
        _fake.RunningAppsToReturn.Add(new RunningAppInfoDto
        {
            ProcessId = 42, ProcessName = "steam", HasVisibleWindow = false,
        });
        _fake.CreateSessionResponseToReturn = new CreateSessionResponse
        {
            SessionId = Guid.NewGuid(), Started = true,
        };

        var vm = NewViewModel();
        await vm.RefreshAsync();
        vm.RunningApps[0].IsSelected = true;
        vm.ManualProcessName = "discord";
        vm.AddManualEntryCommand.Execute(null);
        vm.DurationMinutes = 60;
        vm.SelectedMode = "Strict";

        await vm.StartBlockingAsync();

        _fake.Created.Should().ContainSingle();
        // VM appends .exe to process names that lack the suffix so the
        // WindowsProcessName rule matches what the watcher actually reports.
        _fake.Created[0].Rules!.Select(r => r.Value)
            .Should().BeEquivalentTo(new[] { "steam.exe", "discord.exe" });
        _fake.CreateSessionRequests.Should().ContainSingle();
        var session = _fake.CreateSessionRequests[0];
        session.Mode.Should().Be("Strict");
        session.LockPolicy.Should().Be("TimeLocked");
        session.StartImmediately.Should().BeTrue();
        (session.EndAtUtc - session.StartAtUtc).TotalMinutes.Should().Be(60);

        vm.LastStartSummary.Should().Contain("2 app(s)");
        vm.HasError.Should().BeFalse();
    }

    [Fact]
    public async Task StartBlocking_without_selection_sets_error()
    {
        var vm = NewViewModel();
        await vm.StartBlockingAsync();

        vm.HasError.Should().BeTrue();
        _fake.Created.Should().BeEmpty();
        _fake.CreateSessionRequests.Should().BeEmpty();
    }

    private BlockAppsViewModel NewViewModel() => new(_fake, _clock);
}

internal static class BlockAppsViewModelExtensions
{
    // The OnPropertyChanged for the SelectedCount derived property is normally
    // hit through item bindings; in the test we just call it to settle the value.
    public static void OnPropertyChangedSelectionStub(this BlockAppsViewModel vm)
    {
        // Triggering RefreshAsync also recomputes via OnPropertyChanged paths;
        // this method exists to make the intent of the line explicit in the test.
    }
}
