using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StopMe.App.Core.Services;
using StopMe.LocalApi.Dtos;

namespace StopMe.App.Core.ViewModels;

public partial class ActiveSessionViewModel : ViewModelBase
{
    private readonly IServiceConnection _connection;

    public ActiveSessionViewModel(IServiceConnection connection)
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
            Sessions.Clear();
            foreach (var s in sessions) Sessions.Add(s);
            Focused = Sessions.FirstOrDefault();
        }
        finally
        {
            IsBusy = false;
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
                SetError(_connection.LastError ?? "Service did not respond.");
                return;
            }

            if (!resp.Stopped)
            {
                SetError(resp.DenialReason ?? "Session refused to stop.");
                return;
            }

            Sessions.Remove(Focused);
            Focused = Sessions.FirstOrDefault();
            UserConfirmed = false;
            Password = string.Empty;
            TypedPhrase = string.Empty;
        }
        finally
        {
            IsBusy = false;
        }
    }
}
