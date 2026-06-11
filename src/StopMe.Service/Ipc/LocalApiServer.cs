using System.IO.Pipes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StopMe.LocalApi;
using StopMe.LocalApi.Protocol;

namespace StopMe.Service.Ipc;

public sealed class LocalApiServer
{
    private readonly LocalApiDispatcher _dispatcher;
    private readonly IOptionsMonitor<ServiceOptions> _options;
    private readonly ILogger<LocalApiServer> _logger;

    public LocalApiServer(LocalApiDispatcher dispatcher, IOptionsMonitor<ServiceOptions> options, ILogger<LocalApiServer> logger)
    {
        _dispatcher = dispatcher;
        _options = options;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var concurrency = Math.Max(1, _options.CurrentValue.MaxConcurrentPipeConnections);
        var pipeName = ResolvePipeName();
        _logger.LogInformation("LocalApiServer listening on pipe '{Pipe}' with concurrency {N}.",
            pipeName, concurrency);

        var loops = Enumerable.Range(0, concurrency).Select(_ => AcceptLoopAsync(pipeName, concurrency, ct));
        await Task.WhenAll(loops).ConfigureAwait(false);
    }

    private string ResolvePipeName() =>
        string.IsNullOrWhiteSpace(_options.CurrentValue.PipeNameOverride)
            ? LocalApiProtocol.PipeName
            : _options.CurrentValue.PipeNameOverride!;

    private async Task AcceptLoopAsync(string pipeName, int concurrency, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream pipe;
            try
            {
                pipe = CreateServerStream(pipeName, concurrency);
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "Failed to allocate pipe instance; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(1), ct).ConfigureAwait(false);
                continue;
            }

            await using (pipe)
            {
                try
                {
                    await pipe.WaitForConnectionAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { return; }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "WaitForConnection failed.");
                    continue;
                }

                try
                {
                    await HandleConnectionAsync(pipe, ct).ConfigureAwait(false);
                }
                catch (IOException) { /* client gone */ }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Connection handler crashed.");
                }
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        while (pipe.IsConnected && !ct.IsCancellationRequested)
        {
            LocalApiRequest? request;
            try
            {
                request = await PipeFraming.ReadAsync<LocalApiRequest>(pipe, ct).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Malformed frame received; dropping connection.");
                return;
            }

            if (request is null) return;

            var response = await _dispatcher.DispatchAsync(request, ct).ConfigureAwait(false);
            await PipeFraming.WriteAsync(pipe, response, ct).ConfigureAwait(false);
        }
    }

    private static NamedPipeServerStream CreateServerStream(string pipeName, int maxInstances) =>
        new(
            pipeName: pipeName,
            direction: PipeDirection.InOut,
            maxNumberOfServerInstances: maxInstances,
            transmissionMode: PipeTransmissionMode.Byte,
            options: PipeOptions.Asynchronous | PipeOptions.WriteThrough);
}
