using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Models;
using StopMe.App.Core.Services;

namespace StopMe.App.Core.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly IUserSettingsStore _store;

    public SettingsViewModel(IUserSettingsStore store, IDispatcher? dispatcher = null)
        : base(dispatcher)
    {
        _store = store;
        var state = _store.Load();
        _theme = state.Theme;
        _startAtBoot = state.StartAtBoot;
        _showTrayIcon = state.ShowTrayIcon;
        _dataDirectoryOverride = state.DataDirectoryOverride ?? string.Empty;
    }

    public IReadOnlyList<AppTheme> Themes { get; } = new[] { AppTheme.System, AppTheme.Light, AppTheme.Dark };

    [ObservableProperty]
    private AppTheme _theme;

    [ObservableProperty]
    private bool _startAtBoot;

    [ObservableProperty]
    private bool _showTrayIcon;

    [ObservableProperty]
    private string _dataDirectoryOverride = string.Empty;

    public event EventHandler<AppTheme>? ThemeChanged;

    partial void OnThemeChanged(AppTheme value) => ThemeChanged?.Invoke(this, value);

    [RelayCommand]
    public void Save()
    {
        ClearError();
        _store.Save(new AppSettingsState
        {
            Theme = Theme,
            StartAtBoot = StartAtBoot,
            ShowTrayIcon = ShowTrayIcon,
            DataDirectoryOverride = string.IsNullOrWhiteSpace(DataDirectoryOverride) ? null : DataDirectoryOverride.Trim(),
        });
        StatusMessage = "Saved.";
    }
}
