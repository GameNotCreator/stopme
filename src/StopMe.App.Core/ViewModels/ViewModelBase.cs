using CommunityToolkit.Mvvm.ComponentModel;

namespace StopMe.App.Core.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasError;

    protected void SetError(string message)
    {
        StatusMessage = message;
        HasError = true;
    }

    protected void ClearError()
    {
        StatusMessage = null;
        HasError = false;
    }
}
