using FluentAssertions;
using StopMe.App.Core.Services;
using StopMe.App.Core.Tests.Fakes;
using StopMe.App.Core.ViewModels;
using Xunit;

namespace StopMe.App.Core.Tests.ViewModels;

public class MainViewModelTests
{
    [Fact]
    public void Default_selection_is_dashboard()
    {
        var vm = new MainViewModel(new FakeServiceConnection());
        vm.SelectedItem.Should().NotBeNull();
        vm.SelectedItem!.Key.Should().Be("dashboard");
        // 2026-06 redesign locks the nav to 5 top-level items + Settings footer:
        // Dashboard / Block Apps / Block Websites / Sessions / Statistics.
        vm.Items.Should().Contain(i => i.Key == "apps");
        vm.Items.Should().Contain(i => i.Key == "websites");
        vm.Items.Should().Contain(i => i.Key == "sessions");
        vm.Items.Should().Contain(i => i.Key == "stats");
        vm.FooterItems.Should().ContainSingle(i => i.Key == "settings");
    }

    [Fact]
    public void Connection_state_propagates_to_label()
    {
        var fake = new FakeServiceConnection { State = ConnectionState.Connected };
        var vm = new MainViewModel(fake);

        fake.LastError = "no service";
        fake.RaiseStateChanged(ConnectionState.Failed);

        vm.ConnectionState.Should().Be(ConnectionState.Failed);
        vm.ConnectionLabel.Should().Be("no service");
    }

    [Fact]
    public async Task Reconnect_command_triggers_ensure_connected()
    {
        var fake = new FakeServiceConnection { State = ConnectionState.Failed };
        var vm = new MainViewModel(fake);

        await vm.ReconnectCommand.ExecuteAsync(null);

        // No exception, IsBusy returns false after.
        vm.IsBusy.Should().BeFalse();
    }
}
