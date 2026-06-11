using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using StopMe.Infrastructure.DependencyInjection;
using StopMe.LocalApi.Client;
using StopMe.Service;
using StopMe.Service.Antibypass;
using StopMe.Service.Blocking;
using StopMe.Service.Boot;
using StopMe.Service.Installation;
using StopMe.Service.Ipc;
using StopMe.Service.Ipc.Handlers;
using StopMe.Service.Notifications;
using StopMe.Service.Processes;
using StopMe.Service.Search;
using StopMe.Service.State;

return await Entry.RunAsync(args).ConfigureAwait(false);

internal static class Entry
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length > 0)
        {
            switch (args[0].ToLowerInvariant())
            {
                case "install":
                    return await RunInstall().ConfigureAwait(false);
                case "uninstall":
                    return await ServiceControl.UninstallAsync().ConfigureAwait(false);
                case "start":
                    return await ServiceControl.StartAsync().ConfigureAwait(false);
                case "stop":
                    return await ServiceControl.StopAsync().ConfigureAwait(false);
                case "status":
                    return await ServiceControl.StatusAsync().ConfigureAwait(false);
                case "--help" or "-h":
                    PrintHelp();
                    return 0;
            }
        }

        var dataDir = ResolveDataDirectory(args);
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(Path.Combine(dataDir, "Logs"));

        ConfigureSerilog(dataDir);
        Log.Information("StopMe service starting. DataDirectory={DataDir}", dataDir);

        try
        {
            var builder = Host.CreateApplicationBuilder(args);
            builder.Services.AddWindowsService(o => o.ServiceName = ServiceControl.ServiceName);

            builder.Configuration
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables(prefix: "STOPME_");

            builder.Services.Configure<ServiceOptions>(builder.Configuration.GetSection(ServiceOptions.SectionName));
            builder.Services.Configure<ServiceOptions>(o => o.DataDirectoryOverride = dataDir);
            builder.Services.Configure<AntiBypassOptions>(builder.Configuration.GetSection("AntiBypass"));

            var sqlitePath = Path.Combine(dataDir, "stopme.db");
            builder.Services.AddStopMeInfrastructure($"Data Source={sqlitePath}");

            builder.Services.AddSingleton(new TokenStore(dataDir));
            builder.Services.AddSingleton<IClientAuthenticator, TokenAuthenticator>();
            builder.Services.AddSingleton<IUserContext, DefaultUserContext>();
            builder.Services.AddSingleton<ServiceState>();
            builder.Services.AddSingleton<INotificationSink, LogNotificationSink>();
            builder.Services.AddSingleton<IProcessProvider, WindowsProcessProvider>();
            builder.Services.AddSingleton<IInstalledAppsProvider, WindowsInstalledAppsProvider>();
            builder.Services.AddSingleton<IProcessTerminator, WindowsProcessTerminator>();
            builder.Services.AddSingleton<ProcessWatcher>();
            builder.Services.AddSingleton<BootRestorer>();
            builder.Services.AddSingleton<IExtensionHealthMonitor, ExtensionHealthMonitor>();

            // Blocking pipeline — real hosts-file + loopback-page enforcement.
            // We replace the default BrowserOnly provider registered by
            // AddStopMeInfrastructure with the HostsFile one.
            builder.Services.AddSingleton(sp => new HostsFile(dataDir, sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HostsFile>>()));
            builder.Services.AddSingleton<HostsFileInternetBlockingProvider>();
            builder.Services.Replace(ServiceDescriptor.Singleton<StopMe.Core.Abstractions.IInternetBlockingProvider>(
                sp => sp.GetRequiredService<HostsFileInternetBlockingProvider>()));

            // Keyword search: local, on-device catalogue. The DuckDuckGo HTML
            // provider was removed — it was the only network call in the whole
            // product and posted the user's raw query to a third party,
            // contradicting the privacy promise. To opt-back-in to a remote
            // provider, register an IDomainSearchProvider implementation here
            // and remove this line.
            builder.Services.AddSingleton<IDomainSearchProvider, LocalDomainSearchProvider>();
            builder.Services.AddSingleton<ClockDriftDetector>();
            builder.Services.AddSingleton<AntiBypassRunner>();
            // Replace Core's NullClockDriftMonitor with the real one so SessionEngine
            // can deny strict stops while AntiBypassRunner flags drift on ServiceState.
            builder.Services.AddSingleton<global::StopMe.Core.Abstractions.IClockDriftMonitor,
                                          global::StopMe.Service.Antibypass.ServiceStateClockDriftMonitor>();
            builder.Services.AddSingleton<LocalApiServer>();
            builder.Services.AddSingleton<LocalApiDispatcher>();

            // Handlers — one IOperationHandler registration each.
            builder.Services.AddScoped<IOperationHandler, StatusHandler>();
            builder.Services.AddScoped<IOperationHandler, ActiveSessionsHandler>();
            builder.Services.AddScoped<IOperationHandler, SessionListHandler>();
            builder.Services.AddScoped<IOperationHandler, StartSessionHandler>();
            builder.Services.AddScoped<IOperationHandler, StopSessionHandler>();
            builder.Services.AddScoped<IOperationHandler, ListBlocklistsHandler>();
            builder.Services.AddScoped<IOperationHandler, CreateBlocklistHandler>();
            builder.Services.AddScoped<IOperationHandler, UpdateBlocklistHandler>();
            builder.Services.AddScoped<IOperationHandler, DeleteBlocklistHandler>();
            builder.Services.AddScoped<IOperationHandler, EvaluateUrlHandler>();
            builder.Services.AddScoped<IOperationHandler, EvaluateAppHandler>();
            builder.Services.AddScoped<IOperationHandler, RuleSnapshotHandler>();
            builder.Services.AddScoped<IOperationHandler, ExtensionHeartbeatHandler>();
            builder.Services.AddScoped<IOperationHandler, ExtensionHealthHandler>();
            builder.Services.AddScoped<IOperationHandler, BypassEventsHandler>();
            builder.Services.AddScoped<IOperationHandler, StatsSummaryHandler>();
            builder.Services.AddScoped<IOperationHandler, StatsExportCsvHandler>();
            builder.Services.AddScoped<IOperationHandler, StatsResetHandler>();
            builder.Services.AddScoped<IOperationHandler, SettingsGetHandler>();
            builder.Services.AddScoped<IOperationHandler, SettingsSetHandler>();
            builder.Services.AddScoped<IOperationHandler, TemplatesListHandler>();
            builder.Services.AddScoped<IOperationHandler, TemplatesApplyHandler>();
            builder.Services.AddScoped<IOperationHandler, ProcessSnapshotHandler>();
            builder.Services.AddScoped<IOperationHandler, CreateSessionHandler>();
            builder.Services.AddScoped<IOperationHandler, AddRuleHandler>();
            builder.Services.AddScoped<IOperationHandler, InstalledAppsHandler>();
            builder.Services.AddScoped<IOperationHandler, SearchDomainsHandler>();

            // Hosted services (registration order matters: StartupRoutine runs first).
            builder.Services.AddHostedService<StartupRoutine>();
            builder.Services.AddHostedService<ServiceWorker>();
            builder.Services.AddHostedService<LocalApiHostedService>();
            builder.Services.AddHostedService<LocalBlockedPageServer>();

            builder.Services.AddSerilog();

            using var host = builder.Build();
            await host.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Service terminated unexpectedly.");
            return 1;
        }
        finally
        {
            await Log.CloseAndFlushAsync().ConfigureAwait(false);
        }
    }

    private static string ResolveDataDirectory(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--data-dir", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(args[i + 1]);
        }

        var env = Environment.GetEnvironmentVariable("STOPME_DATA_DIR");
        if (!string.IsNullOrWhiteSpace(env)) return Path.GetFullPath(env);

        // Always default to %ProgramData%\StopMe so the WinUI app, NativeHost,
        // and console-mode service share the same auth.token. Without this,
        // dev-mode boots end up with the service writing to <bin>\.stopme-data
        // while clients look in %ProgramData% — connection fails silently.
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "StopMe");
    }

    private static void ConfigureSerilog(string dataDir)
    {
        var logFile = Path.Combine(dataDir, "Logs", "stopme-service-.log");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                path: logFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 50 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .CreateLogger();
    }

    [SupportedOSPlatform("windows")]
    private static async Task<int> RunInstall()
    {
        var exe = Assembly.GetEntryAssembly()?.Location;
        if (string.IsNullOrEmpty(exe) || exe.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            // When packaged as a single-file native executable, Location is empty.
            exe = Environment.ProcessPath;
        }
        if (string.IsNullOrEmpty(exe))
        {
            Console.Error.WriteLine("Could not determine the path to StopMe.Service.exe.");
            return 1;
        }
        return await ServiceControl.InstallAsync(exe).ConfigureAwait(false);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("StopMe Service");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  StopMe.Service.exe                  Run in console / Windows Service mode");
        Console.WriteLine("  StopMe.Service.exe --data-dir PATH  Override data directory");
        Console.WriteLine();
        Console.WriteLine("Service management (require admin):");
        Console.WriteLine("  StopMe.Service.exe install          Register as a Windows Service");
        Console.WriteLine("  StopMe.Service.exe uninstall        Unregister the Windows Service");
        Console.WriteLine("  StopMe.Service.exe start            Start the Windows Service");
        Console.WriteLine("  StopMe.Service.exe stop             Stop the Windows Service");
        Console.WriteLine("  StopMe.Service.exe status           Query service status");
    }
}
