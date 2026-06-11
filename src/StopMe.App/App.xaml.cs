using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using StopMe.App.Core.Services;
using StopMe.App.Core.ViewModels;
using StopMe.App.Services;

namespace StopMe.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static MainWindow? MainWindow { get; private set; }

    public App()
    {
        InitializeComponent();
        Services = BuildServices();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = Services.GetRequiredService<MainWindow>();
        // Locked to Light per the 2026-06 design handoff.
        if (MainWindow.Content is FrameworkElement root) root.RequestedTheme = ElementTheme.Light;
        MainWindow.Activate();
        _ = Services.GetRequiredService<IServiceConnection>().EnsureConnectedAsync();
    }

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Information));

        var settingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StopMe");

        services.AddSingleton<IUserSettingsStore>(_ => new JsonUserSettingsStore(settingsDir));
        services.AddSingleton<IStatsCsvWriter>(_ => new DocumentsStatsCsvWriter());

        services.AddSingleton<IOptions<ServiceConnectionOptions>>(sp =>
        {
            var store = sp.GetRequiredService<IUserSettingsStore>();
            var state = store.Load();
            return Options.Create(new ServiceConnectionOptions
            {
                DataDirectoryOverride = state.DataDirectoryOverride,
            });
        });

        services.AddSingleton<IServiceConnection, ServiceConnection>();
        services.AddSingleton<TimeProvider>(TimeProvider.System);

        // IDispatcher is captured from the UI thread at construction. Without
        // it, ViewModels that mutate ObservableCollections after an await run
        // those mutations on a pool thread and crash WinUI with
        // RPC_E_WRONG_THREAD. App.OnLaunched runs on the UI thread, so
        // DispatcherQueue.GetForCurrentThread() returns the right queue.
        services.AddSingleton<IDispatcher>(_ =>
            new DispatcherQueueDispatcher(DispatcherQueue.GetForCurrentThread()));

        // === ViewModels ===
        services.AddSingleton<MainViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<BlockAppsViewModel>();
        services.AddTransient<BlockWebsitesViewModel>();
        services.AddTransient<SessionsViewModel>();
        services.AddTransient<StatisticsViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Kept for backward compat (Blocklists / Templates / Planner / ActiveSession routes
        // may still be triggered from external entry points; the new sidebar collapses them
        // into the 5-nav-item design).
        services.AddTransient<ActiveSessionViewModel>();
        services.AddTransient<BlocklistsViewModel>();
        services.AddTransient<PlannerViewModel>();
        services.AddTransient<TemplatesViewModel>();

        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }
}
