using Microsoft.EntityFrameworkCore;
using StopMe.Core.Models;

namespace StopMe.Infrastructure.Persistence;

public class StopMeDbContext : DbContext
{
    public StopMeDbContext(DbContextOptions<StopMeDbContext> options) : base(options) { }

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Blocklist> Blocklists => Set<Blocklist>();
    public DbSet<BlockRule> BlockRules => Set<BlockRule>();
    public DbSet<FocusSession> FocusSessions => Set<FocusSession>();
    public DbSet<SessionBlocklist> SessionBlocklists => Set<SessionBlocklist>();
    public DbSet<PausePolicy> PausePolicies => Set<PausePolicy>();
    public DbSet<UsageEvent> UsageEvents => Set<UsageEvent>();
    public DbSet<CategoryDomain> CategoryDomains => Set<CategoryDomain>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<BypassEvent> BypassEvents => Set<BypassEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StopMeDbContext).Assembly);
    }
}
