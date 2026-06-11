using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StopMe.Service.Processes;

public sealed class WindowsProcessTerminator : IProcessTerminator
{
    private readonly IOptionsMonitor<ServiceOptions> _options;
    private readonly ILogger<WindowsProcessTerminator> _logger;

    public WindowsProcessTerminator(IOptionsMonitor<ServiceOptions> options, ILogger<WindowsProcessTerminator> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task<TerminationResult> CloseOrKillAsync(int processId, string processName, CancellationToken ct)
    {
        Process? process = null;
        try
        {
            process = Process.GetProcessById(processId);
        }
        catch (ArgumentException)
        {
            return TerminationResult.AlreadyGone;
        }
        catch (InvalidOperationException)
        {
            return TerminationResult.AlreadyGone;
        }

        try
        {
            var timeout = TimeSpan.FromSeconds(_options.CurrentValue.GracefulCloseTimeoutSeconds);

            if (HasGracefulHandle(process) && TryCloseMainWindow(process))
            {
                if (await WaitForExitAsync(process, timeout, ct).ConfigureAwait(false))
                {
                    _logger.LogInformation("Process {Pid} ({Name}) closed gracefully.", processId, processName);
                    return TerminationResult.GracefullyClosed;
                }
            }

            process.Kill(entireProcessTree: true);
            await WaitForExitAsync(process, TimeSpan.FromSeconds(2), ct).ConfigureAwait(false);
            _logger.LogInformation("Process {Pid} ({Name}) killed.", processId, processName);
            return TerminationResult.Killed;
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            _logger.LogWarning("Access denied terminating process {Pid} ({Name}).", processId, processName);
            return TerminationResult.AccessDenied;
        }
        catch (InvalidOperationException)
        {
            return TerminationResult.AlreadyGone;
        }
        catch (Win32Exception ex)
        {
            _logger.LogWarning(ex, "Failed to terminate process {Pid} ({Name}).", processId, processName);
            return TerminationResult.Failed;
        }
        finally
        {
            process.Dispose();
        }
    }

    private static bool HasGracefulHandle(Process p)
    {
        try { return p.MainWindowHandle != IntPtr.Zero; }
        catch (InvalidOperationException) { return false; }
    }

    private static bool TryCloseMainWindow(Process p)
    {
        try { return p.CloseMainWindow(); }
        catch (InvalidOperationException) { return false; }
        catch (Win32Exception) { return false; }
    }

    private static async Task<bool> WaitForExitAsync(Process p, TimeSpan timeout, CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            await p.WaitForExitAsync(cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
