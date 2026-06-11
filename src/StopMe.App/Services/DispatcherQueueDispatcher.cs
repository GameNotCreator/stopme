using Microsoft.UI.Dispatching;
using StopMe.App.Core.Services;

namespace StopMe.App.Services;

// Bridges the WinUI 3 DispatcherQueue onto the App.Core IDispatcher abstraction
// so ViewModels (which live in App.Core, free of any WinUI dependency) can
// marshal collection mutations and post-await property writes to the UI thread
// without crashing with RPC_E_WRONG_THREAD.
public sealed class DispatcherQueueDispatcher : IDispatcher
{
    private readonly DispatcherQueue _queue;

    public DispatcherQueueDispatcher(DispatcherQueue queue)
    {
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
    }

    public bool HasThreadAccess => _queue.HasThreadAccess;

    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_queue.HasThreadAccess)
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = _queue.TryEnqueue(() =>
        {
            try { action(); tcs.SetResult(); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        if (!queued)
            tcs.SetException(new InvalidOperationException("DispatcherQueue refused to enqueue the action."));
        return tcs.Task;
    }

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_queue.HasThreadAccess)
            return Task.FromResult(action());

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queued = _queue.TryEnqueue(() =>
        {
            try { tcs.SetResult(action()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        if (!queued)
            tcs.SetException(new InvalidOperationException("DispatcherQueue refused to enqueue the func."));
        return tcs.Task;
    }
}
