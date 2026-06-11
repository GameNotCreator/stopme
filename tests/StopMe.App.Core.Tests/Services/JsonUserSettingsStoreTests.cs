using FluentAssertions;
using StopMe.App.Core.Models;
using StopMe.App.Core.Services;
using Xunit;

namespace StopMe.App.Core.Tests.Services;

public class JsonUserSettingsStoreTests : IDisposable
{
    private readonly string _dir;

    public JsonUserSettingsStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "stopme-test-" + Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public void Load_returns_default_when_file_missing()
    {
        var store = new JsonUserSettingsStore(_dir);
        var state = store.Load();
        state.Should().NotBeNull();
        state.Theme.Should().Be(AppTheme.System);
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var store = new JsonUserSettingsStore(_dir);
        store.Save(new AppSettingsState
        {
            Theme = AppTheme.Dark,
            StartAtBoot = true,
            ShowTrayIcon = false,
            DataDirectoryOverride = @"C:\Custom",
        });

        var roundTripped = new JsonUserSettingsStore(_dir).Load();
        roundTripped.Theme.Should().Be(AppTheme.Dark);
        roundTripped.StartAtBoot.Should().BeTrue();
        roundTripped.ShowTrayIcon.Should().BeFalse();
        roundTripped.DataDirectoryOverride.Should().Be(@"C:\Custom");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); }
        catch { /* best-effort */ }
        GC.SuppressFinalize(this);
    }
}
