using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StopMe.Core.Models;
using StopMe.Infrastructure.Persistence;
using StopMe.Infrastructure.Seed;
using StopMe.LocalApi.Client;
using StopMe.Service.Installation;
using StopMe.Service.Ipc;

namespace StopMe.Service.Boot;

public sealed class StartupRoutine : IHostedService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TokenStore _tokenStore;
    private readonly TimeProvider _clock;
    private readonly ILogger<StartupRoutine> _logger;

    public StartupRoutine(IServiceScopeFactory scopes, TokenStore tokenStore, TimeProvider clock, ILogger<StartupRoutine> logger)
    {
        _scopes = scopes;
        _tokenStore = tokenStore;
        _clock = clock;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<StopMeDbContext>();
        _logger.LogInformation("Applying database migrations…");
        await db.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);

        await EnsureDefaultUserAsync(db, cancellationToken).ConfigureAwait(false);

        var seeder = sp.GetRequiredService<CategorySeeder>();
        var added = await seeder.SeedAsync(cancellationToken).ConfigureAwait(false);
        if (added > 0)
            _logger.LogInformation("Seeded {Added} category domains.", added);

        // Ensure the bearer token exists and is ACL-locked. The first call also
        // generates it; subsequent boots only confirm + re-harden.
        _ = _tokenStore.LoadOrCreate();
        if (OperatingSystem.IsWindows())
            TokenAclHardener.TryHarden(_tokenStore.FilePath, _logger);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureDefaultUserAsync(StopMeDbContext db, CancellationToken ct)
    {
        var existing = await db.UserProfiles.FindAsync(new object[] { DefaultUserContext.DefaultId }, ct).ConfigureAwait(false);
        if (existing is not null) return;

        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        db.UserProfiles.Add(new UserProfile
        {
            Id = DefaultUserContext.DefaultId,
            DisplayName = "Local User",
            IsLocalOnly = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        });
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Created default local user profile.");
    }
}
