using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Models;
using StopMe.App.Core.Services;

namespace StopMe.App.Core.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly IServiceConnection _connection;

    public MainViewModel(IServiceConnection connection, IDispatcher? dispatcher = null)
        : base(dispatcher)
    {
        _connection = connection;
        _connection.StateChanged += OnConnectionStateChanged;
        ConnectionState = _connection.State;

        // The 6 top-level nav items locked by the 2026-06 design handoff:
        // Dashboard / Block Apps / Block Websites / Sessions / Statistics / Settings.
        // Icon names map to StopMe.App.Controls.IconGlyph entries.
        Items = new ObservableCollection<NavigationItem>
        {
            new("dashboard", "Dashboard",       "dashboard"),
            new("apps",      "Block Apps",      "apps"),
            new("websites",  "Block Websites",  "globe"),
            new("sessions",  "Sessions",        "sessions"),
            new("stats",     "Statistics",      "stats"),
        };

        // Settings sits in the sidebar footer per spec.
        FooterItems = new ObservableCollection<NavigationItem>
        {
            new("settings", "Settings", "settings", IsFooter: true),
        };

        SelectedItem = Items[0];
    }

    public ObservableCollection<NavigationItem> Items { get; }

    public ObservableCollection<NavigationItem> FooterItems { get; }

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    [ObservableProperty]
    private ConnectionState _connectionState;

    [ObservableProperty]
    private string _connectionLabel = "Disconnected";

    [RelayCommand]
    private async Task ReconnectAsync(CancellationToken ct)
    {
        IsBusy = true;
        try
        {
            await _connection.EnsureConnectedAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnConnectionStateChanged(object? sender, EventArgs e)
    {
        // The connection state event can fire from a background thread
        // (e.g. EnsureConnectedAsync continuation). Marshal the property
        // updates to the UI thread so x:Bind / classic Binding don't crash.
        _ = Dispatcher.InvokeAsync(() =>
        {
            ConnectionState = _connection.State;
            ConnectionLabel = _connection.State switch
            {
                Services.ConnectionState.Connected => "Service connected",
                Services.ConnectionState.Connecting => "Connecting to StopMe service…",
                Services.ConnectionState.Failed => _connection.LastError ?? "Service issue — click for details",
                _ => "Disconnected",
            };
        });
    }
}
