using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Services;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IServiceConnection _connection;

    public DashboardViewModel(IServiceConnection connection, IDispatcher? dispatcher = null)
        : base(dispatcher)
    {
        _connection = connection;
    }

    public ObservableCollection<ActiveSessionDto> ActiveSessions { get; } = new();

    public ObservableCollection<BypassEventDto> RecentBypassEvents { get; } = new();

    [ObservableProperty]
    private int _blocklistCount;

    [ObservableProperty]
    private DateTime _lastTickUtc;

    [ObservableProperty]
    private bool _bootRestoreCompleted;

    [ObservableProperty]
    private string? _serviceVersion;

    [ObservableProperty]
    private bool _serviceReachable;

    [ObservableProperty]
    private string? _serviceStatusLabel;

    public bool HasNoActiveSession => ActiveSessions.Count == 0;

    public bool HasNoBypassEvents => RecentBypassEvents.Count == 0;

    public bool HasActiveSession => ActiveSessions.Count > 0;

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        ClearError();
        IsBusy = true;
        try
        {
            var status = await _connection.GetStatusAsync(ct).ConfigureAwait(false);
            if (status is null)
            {
                await RunOnUiAsync(() =>
                {
                    ServiceReachable = false;
                    ServiceStatusLabel = _connection.LastError ?? "StopMe service is not reachable.";
                    SetError(_connection.LastError ?? "Service is unreachable.");
                }).ConfigureAwait(false);
                return;
            }

            var sessions = await _connection.GetActiveSessionsAsync(ct).ConfigureAwait(false);
            var blocklists = await _connection.ListBlocklistsAsync(ct).ConfigureAwait(false);
            var bypass = await _connection.GetBypassEventsAsync(ct).ConfigureAwait(false);

            await RunOnUiAsync(() =>
            {
                ServiceReachable = true;
                ServiceVersion = status.ServiceVersion;
                LastTickUtc = status.LastTickUtc;
                BootRestoreCompleted = status.BootRestoreCompleted;
                ServiceStatusLabel = $"Connected — v{status.ServiceVersion}, last tick {status.LastTickUtc:HH:mm:ss} UTC, "
                    + $"{status.ActiveSessionCount} active session(s).";

                ActiveSessions.Clear();
                foreach (var s in sessions) ActiveSessions.Add(s);
                OnPropertyChanged(nameof(HasNoActiveSession));
                OnPropertyChanged(nameof(HasActiveSession));

                BlocklistCount = blocklists.Count;

                RecentBypassEvents.Clear();
                foreach (var ev in bypass.Take(10)) RecentBypassEvents.Add(ev);
                OnPropertyChanged(nameof(HasNoBypassEvents));
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }
}
