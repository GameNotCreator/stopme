using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Events;
using StopMe.NativeHost;

return await Entry.RunAsync(args).ConfigureAwait(false);

internal static class Entry
{
    public static async Task<int> RunAsync(string[] args)
    {
        // The Chromium browser launches us with the manifest path + origin as args. We
        // ignore them — they're useful for diagnostics only. Logging goes to a per-user
        // file because stdout is reserved for native messaging frames; never write to it
        // outside of the channel.
        var options = NativeHostOptions.Resolve();
        ConfigureSerilog(options.DataDirectory);
        Log.Information("StopMe.NativeHost starting. DataDirectory={DataDir} Args={Args}",
            options.DataDirectory, string.Join(' ', args));

        try
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSerilog(Log.Logger, dispose: false);
                builder.SetMinimumLevel(LogLevel.Information);
            });

            await using var bridge = new LocalApiServiceBridge(options,
                loggerFactory.CreateLogger<LocalApiServiceBridge>());
            var dispatcher = new ExtensionMessageDispatcher(bridge,
                loggerFactory.CreateLogger<ExtensionMessageDispatcher>());

            using var stdin = Console.OpenStandardInput();
            using var stdout = Console.OpenStandardOutput();
            var channel = new NativeMessagingChannel(stdin, stdout);

            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

            while (!cts.IsCancellationRequested)
            {
                System.Text.Json.JsonDocument? request;
                try
                {
                    request = await channel.ReadAsync(cts.Token).ConfigureAwait(false);
                }
                catch (InvalidOperationException ex)
                {
                    Log.Warning("Bad frame: {Reason}", ex.Message);
                    return 1;
                }

                if (request is null)
                {
                    Log.Information("Browser closed the channel.");
                    return 0;
                }

                using (request)
                {
                    var response = await dispatcher.DispatchAsync(request, cts.Token).ConfigureAwait(false);
                    await channel.WriteAsync(response, cts.Token).ConfigureAwait(false);
                }
            }
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "NativeHost terminated unexpectedly.");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync().ConfigureAwait(false);
        }
    }

    private static void ConfigureSerilog(string dataDir)
    {
        var logsDir = Path.Combine(dataDir, "Logs");
        try { Directory.CreateDirectory(logsDir); }
        catch { /* best-effort; logging may end up disabled */ }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .Enrich.FromLogContext()
            .WriteTo.File(
                path: Path.Combine(logsDir, "stopme-nativehost-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();
    }
}
