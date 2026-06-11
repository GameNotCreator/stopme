namespace StopMe.App.Core.Services;

public interface IDispatcher
{
    bool HasThreadAccess { get; }

    Task InvokeAsync(Action action);

    Task<T> InvokeAsync<T>(Func<T> action);
}

// Synchronous fallback for non-UI environments (tests, console hosts).
public sealed class ImmediateDispatcher : IDispatcher
{
    public bool HasThreadAccess => true;

    public Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        action();
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return Task.FromResult(action());
    }
}
