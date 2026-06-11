using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using StopMe.Core.Abstractions;
using StopMe.Core.Engines;
using StopMe.Infrastructure.Persistence;
using StopMe.Infrastructure.Repositories;
using StopMe.Infrastructure.Seed;
using StopMe.LocalApi.Client;
using StopMe.Service;
using StopMe.Service.Antibypass;
using StopMe.Service.Boot;
using StopMe.Service.Ipc;
using StopMe.Service.Ipc.Handlers;
using StopMe.Service.Notifications;
using StopMe.Service.Processes;
using StopMe.Service.State;

namespace StopMe.Service.Tests.Fakes;

internal sealed class TestHost : IAsyncDisposable
{
    public SqliteConnection Connection { get; }
    public ServiceProvider Services { get; }
    public FakeTimeProvider Clock { get; }
    public ServiceOptions Options { get; }
    public AntiBypassOptions AntiBypassOptions { get; }
    public FakeProcessProvider ProcessProvider { get; }
    public FakeProcessTerminator ProcessTerminator { get; }
    public FakeNotificationSink Notifications { get; }
    public string Token { get; }
    public string DataDir { get; }

    public TestHost(DateTime? nowUtc = null, ServiceOptions? options = null, string? token = null, bool ensureCreated = true)
    {
        Connection = new SqliteConnection("DataSource=:memory:");
        Connection.Open();

        var fixedNow = nowUtc ?? new DateTime(2026, 6, 8, 10, 0, 0, DateTimeKind.Utc);
        Clock = new FakeTimeProvider(new DateTimeOffset(fixedNow, TimeSpan.Zero));

        Options = options ?? new ServiceOptions { TickIntervalSeconds = 2, GracefulCloseTimeoutSeconds = 1 };
        AntiBypassOptions = new AntiBypassOptions { ExtensionStaleAfterSeconds = 60, ClockDriftThresholdSeconds = 60 };
        ProcessProvider = new FakeProcessProvider();
        ProcessTerminator = new FakeProcessTerminator();
        Notifications = new FakeNotificationSink();
        Token = token ?? "TEST-TOKEN";

        DataDir = Path.Combine(Path.GetTempPath(), "stopme-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DataDir);

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<StopMeDbContext>(o => o.UseSqlite(Connection));

        services.AddScoped<IBlocklistRepository, BlocklistRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IUsageEventRepository, UsageEventRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBypassEventRepository, BypassEventRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IUsageEventRecorder, UsageEventRecorder>();
        services.AddSingleton<IStatsAggregator, StatsAggregator>();
        services.AddScoped<CategorySeeder>();

        services.AddSingleton<TimeProvider>(Clock);
        services.AddSingleton<IUrlRuleEngine, UrlRuleEngine>();
        services.AddSingleton<IAppRuleEngine, AppRuleEngine>();
        services.AddScoped<IRuleCompiler, RuleCompiler>();
        services.AddScoped<ISessionEngine, SessionEngine>();
        services.AddSingleton<IStrictModeGuard, StrictModeGuard>();
        services.AddScoped<ITemplateService, TemplateService>();
        services.AddSingleton<IPauseEngine, PomodoroPauseEngine>();
        services.AddSingleton<IInternetBlockingProvider, BrowserOnlyInternetBlockingProvider>();

        services.AddSingleton(Options);
        services.AddSingleton<IOptionsMonitor<ServiceOptions>>(new TestOptionsMonitor<ServiceOptions>(Options));
        services.AddSingleton(AntiBypassOptions);
        services.AddSingleton<IOptionsMonitor<AntiBypassOptions>>(new TestOptionsMonitor<AntiBypassOptions>(AntiBypassOptions));

        services.AddSingleton(new TokenStore(DataDir));
        services.AddSingleton<IClientAuthenticator>(new ConstantTokenAuthenticator(Token));
        services.AddSingleton<IUserContext, DefaultUserContext>();
        services.AddSingleton<ServiceState>();
        services.AddSingleton<INotificationSink>(Notifications);
        services.AddSingleton<IProcessProvider>(ProcessProvider);
        services.AddSingleton<IProcessTerminator>(ProcessTerminator);
        services.AddSingleton<ProcessWatcher>();
        services.AddSingleton<BootRestorer>();
        services.AddSingleton<IExtensionHealthMonitor, ExtensionHealthMonitor>();
        services.AddSingleton<ClockDriftDetector>();
        services.AddSingleton<AntiBypassRunner>();
        services.AddSingleton<IClockDriftMonitor, ServiceStateClockDriftMonitor>();
        services.AddSingleton<LocalApiDispatcher>();
        services.AddSingleton<LocalApiServer>();

        services.AddScoped<IOperationHandler, StatusHandler>();
        services.AddScoped<IOperationHandler, ActiveSessionsHandler>();
        services.AddScoped<IOperationHandler, StartSessionHandler>();
        services.AddScoped<IOperationHandler, StopSessionHandler>();
        services.AddScoped<IOperationHandler, ListBlocklistsHandler>();
        services.AddScoped<IOperationHandler, CreateBlocklistHandler>();
        services.AddScoped<IOperationHandler, UpdateBlocklistHandler>();
        services.AddScoped<IOperationHandler, DeleteBlocklistHandler>();
        services.AddScoped<IOperationHandler, EvaluateUrlHandler>();
        services.AddScoped<IOperationHandler, EvaluateAppHandler>();
        services.AddScoped<IOperationHandler, RuleSnapshotHandler>();
        services.AddScoped<IOperationHandler, ExtensionHeartbeatHandler>();
        services.AddScoped<IOperationHandler, ExtensionHealthHandler>();
        services.AddScoped<IOperationHandler, BypassEventsHandler>();
        services.AddScoped<IOperationHandler, StatsSummaryHandler>();
        services.AddScoped<IOperationHandler, StatsExportCsvHandler>();
        services.AddScoped<IOperationHandler, StatsResetHandler>();
        services.AddScoped<IOperationHandler, SettingsGetHandler>();
        services.AddScoped<IOperationHandler, SettingsSetHandler>();
        services.AddScoped<IOperationHandler, TemplatesListHandler>();
        services.AddScoped<IOperationHandler, TemplatesApplyHandler>();
        services.AddScoped<IOperationHandler, ProcessSnapshotHandler>();
        services.AddScoped<IOperationHandler, CreateSessionHandler>();
        services.AddScoped<IOperationHandler, AddRuleHandler>();

        Services = services.BuildServiceProvider();

        if (ensureCreated)
        {
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<StopMeDbContext>().Database.EnsureCreated();
        }
    }

    public AsyncServiceScope CreateScope() => Services.CreateAsyncScope();

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync().ConfigureAwait(false);
        Connection.Dispose();
        try { if (Directory.Exists(DataDir)) Directory.Delete(DataDir, recursive: true); } catch { }
    }
}

internal sealed class TestOptionsMonitor<T> : IOptionsMonitor<T> where T : class
{
    public TestOptionsMonitor(T value) => CurrentValue = value;

    public T CurrentValue { get; }

    public T Get(string? name) => CurrentValue;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}

internal sealed class ConstantTokenAuthenticator : IClientAuthenticator
{
    private readonly string _expected;
    public ConstantTokenAuthenticator(string expected) => _expected = expected;
    public bool IsAuthorized(string? token) => string.Equals(token, _expected, StringComparison.Ordinal);
}
