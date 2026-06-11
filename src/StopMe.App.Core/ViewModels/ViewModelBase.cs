using CommunityToolkit.Mvvm.ComponentModel;
using StopMe.App.Core.Services;

namespace StopMe.App.Core.ViewModels;

public abstract partial class ViewModelBase : ObservableObject
{
    private readonly IDispatcher _dispatcher;

    protected ViewModelBase(IDispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher ?? new ImmediateDispatcher();
    }

    protected IDispatcher Dispatcher => _dispatcher;

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

    // Marshals collection mutations and post-await property writes onto the UI
    // dispatcher. In tests / non-UI hosts, the ImmediateDispatcher fallback
    // runs `action` inline so the existing test suite is unaffected.
    protected Task RunOnUiAsync(Action action) => _dispatcher.InvokeAsync(action);

    protected Task<T> RunOnUiAsync<T>(Func<T> action) => _dispatcher.InvokeAsync(action);
}
