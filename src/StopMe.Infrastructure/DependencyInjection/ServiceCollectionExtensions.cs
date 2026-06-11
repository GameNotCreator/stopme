using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StopMe.Core.Abstractions;
using StopMe.Core.Engines;
using StopMe.Infrastructure.Persistence;
using StopMe.Infrastructure.Repositories;
using StopMe.Infrastructure.Seed;

namespace StopMe.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddStopMeInfrastructure(this IServiceCollection services, string sqliteConnectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(sqliteConnectionString);

        services.AddDbContext<StopMeDbContext>(options =>
            options.UseSqlite(sqliteConnectionString));

        services.AddScoped<IBlocklistRepository, BlocklistRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IUsageEventRepository, UsageEventRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IBypassEventRepository, BypassEventRepository>();
        services.AddScoped<ISettingsRepository, SettingsRepository>();
        services.AddScoped<IUsageEventRecorder, UsageEventRecorder>();
        services.AddSingleton<IStatsAggregator, StatsAggregator>();
        services.AddScoped<CategorySeeder>();

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IRuleCompiler, RuleCompiler>();
        services.AddSingleton<IUrlRuleEngine, UrlRuleEngine>();
        services.AddSingleton<IAppRuleEngine, AppRuleEngine>();
        services.AddScoped<ISessionEngine, SessionEngine>();
        services.AddSingleton<IStrictModeGuard, StrictModeGuard>();
        services.AddScoped<ITemplateService, TemplateService>();
        // PomodoroPauseEngine handles Pomodoro sessions and falls back to a permanent
        // Focus phase for any other PauseType, so it's a safe drop-in default.
        services.AddSingleton<Core.Abstractions.IPauseEngine, Core.Engines.PomodoroPauseEngine>();
        services.AddSingleton<Core.Abstractions.IInternetBlockingProvider, Core.Engines.BrowserOnlyInternetBlockingProvider>();

        return services;
    }
}
