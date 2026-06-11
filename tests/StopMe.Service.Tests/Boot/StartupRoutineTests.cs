using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StopMe.Infrastructure.Persistence;
using StopMe.Service.Boot;
using StopMe.Service.Ipc;
using StopMe.Service.Tests.Fakes;
using Xunit;

namespace StopMe.Service.Tests.Boot;

public class StartupRoutineTests
{
    [Fact]
    public async Task StartAsync_creates_default_user_and_seeds_categories()
    {
        await using var host = new TestHost(ensureCreated: false);
        var routine = ActivatorUtilities.CreateInstance<StartupRoutine>(host.Services);

        await routine.StartAsync(CancellationToken.None);

        await using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();

        (await db.UserProfiles.FindAsync(DefaultUserContext.DefaultId)).Should().NotBeNull();
        (await db.CategoryDomains.AnyAsync(c => c.CategoryName == "Social Media")).Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_is_idempotent()
    {
        await using var host = new TestHost(ensureCreated: false);
        var routine = ActivatorUtilities.CreateInstance<StartupRoutine>(host.Services);

        await routine.StartAsync(CancellationToken.None);
        await routine.StartAsync(CancellationToken.None);

        await using var scope = host.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StopMeDbContext>();
        (await db.UserProfiles.CountAsync(u => u.Id == DefaultUserContext.DefaultId)).Should().Be(1);
    }
}
