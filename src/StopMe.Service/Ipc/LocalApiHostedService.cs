using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace StopMe.Service.Ipc;

public sealed class LocalApiHostedService : BackgroundService
{
    private readonly LocalApiServer _server;
    private readonly ILogger<LocalApiHostedService> _logger;

    public LocalApiHostedService(LocalApiServer server, ILogger<LocalApiHostedService> logger)
    {
        _server = server;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _server.RunAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* shutdown */ }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "LocalApiServer crashed; service will continue without IPC.");
        }
    }
}
