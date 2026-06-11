using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Services;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.ViewModels;

public partial class ActiveSessionViewModel : ViewModelBase
{
    private readonly IServiceConnection _connection;

    public ActiveSessionViewModel(IServiceConnection connection, IDispatcher? dispatcher = null)
        : base(dispatcher)
    {
        _connection = connection;
    }

    public ObservableCollection<ActiveSessionDto> Sessions { get; } = new();

    [ObservableProperty]
    private ActiveSessionDto? _focused;

    [ObservableProperty]
    private string _typedPhrase = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _userConfirmed;

    [RelayCommand]
    public async Task RefreshAsync(CancellationToken ct = default)
    {
        ClearError();
        IsBusy = true;
        try
        {
            var sessions = await _connection.GetActiveSessionsAsync(ct).ConfigureAwait(false);
            await RunOnUiAsync(() =>
            {
                Sessions.Clear();
                foreach (var s in sessions) Sessions.Add(s);
                Focused = Sessions.FirstOrDefault();
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }

    [RelayCommand]
    public async Task StopSessionAsync(CancellationToken ct = default)
    {
        if (Focused is null) return;
        ClearError();
        IsBusy = true;
        try
        {
            var resp = await _connection.StopSessionAsync(new StopSessionRequest
            {
                SessionId = Focused.SessionId,
                Reason = "UserRequested",
                UserConfirmed = UserConfirmed,
                Password = string.IsNullOrEmpty(Password) ? null : Password,
                TypedText = string.IsNullOrEmpty(TypedPhrase) ? null : TypedPhrase,
            }, ct).ConfigureAwait(false);

            if (resp is null)
            {
                await RunOnUiAsync(() =>
                    SetError(_connection.LastError ?? "Service did not respond.")
                ).ConfigureAwait(false);
                return;
            }

            if (!resp.Stopped)
            {
                await RunOnUiAsync(() =>
                    SetError(resp.DenialReason ?? "Session refused to stop.")
                ).ConfigureAwait(false);
                return;
            }

            await RunOnUiAsync(() =>
            {
                Sessions.Remove(Focused);
                Focused = Sessions.FirstOrDefault();
                UserConfirmed = false;
                Password = string.Empty;
                TypedPhrase = string.Empty;
            }).ConfigureAwait(false);
        }
        finally
        {
            await RunOnUiAsync(() => IsBusy = false).ConfigureAwait(false);
        }
    }
}
