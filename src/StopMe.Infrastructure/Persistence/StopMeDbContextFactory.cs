using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace StopMe.Infrastructure.Persistence;

// Used by `dotnet ef` to instantiate the DbContext at design time so we can run migrations
// without a running host.
internal sealed class StopMeDbContextFactory : IDesignTimeDbContextFactory<StopMeDbContext>
{
    public StopMeDbContext CreateDbContext(string[] args)
    {
        var dbPath = Environment.GetEnvironmentVariable("STOPME_DB_PATH")
            ?? Path.Combine(Path.GetTempPath(), "stopme-design.db");
        var options = new DbContextOptionsBuilder<StopMeDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        return new StopMeDbContext(options);
    }
}
