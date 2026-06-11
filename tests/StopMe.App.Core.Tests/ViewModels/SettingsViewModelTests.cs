using FluentAssertions;
using StopMe.App.Core.Models;
using StopMe.App.Core.Services;
using StopMe.App.Core.ViewModels;
using Xunit;

namespace StopMe.App.Core.Tests.ViewModels;

public class SettingsViewModelTests
{
    [Fact]
    public void Save_writes_through_store()
    {
        var store = new InMemorySettingsStore();
        var vm = new SettingsViewModel(store);

        vm.Theme = AppTheme.Dark;
        vm.StartAtBoot = true;
        vm.ShowTrayIcon = false;
        vm.DataDirectoryOverride = @"C:\Custom";

        vm.Save();

        store.Saved.Should().NotBeNull();
        store.Saved!.Theme.Should().Be(AppTheme.Dark);
        store.Saved.StartAtBoot.Should().BeTrue();
        store.Saved.ShowTrayIcon.Should().BeFalse();
        store.Saved.DataDirectoryOverride.Should().Be(@"C:\Custom");
    }

    [Fact]
    public void Theme_change_raises_event()
    {
        var store = new InMemorySettingsStore();
        var vm = new SettingsViewModel(store);

        AppTheme? observed = null;
        vm.ThemeChanged += (_, t) => observed = t;

        vm.Theme = AppTheme.Light;

        observed.Should().Be(AppTheme.Light);
    }

    [Fact]
    public void Constructor_loads_existing_state()
    {
        var store = new InMemorySettingsStore
        {
            Initial = new AppSettingsState { Theme = AppTheme.Dark, ShowTrayIcon = false },
        };

        var vm = new SettingsViewModel(store);

        vm.Theme.Should().Be(AppTheme.Dark);
        vm.ShowTrayIcon.Should().BeFalse();
    }

    private sealed class InMemorySettingsStore : IUserSettingsStore
    {
        public AppSettingsState Initial { get; set; } = new();
        public AppSettingsState? Saved { get; private set; }
        public AppSettingsState Load() => Initial;
        public void Save(AppSettingsState state) => Saved = state;
    }
}
