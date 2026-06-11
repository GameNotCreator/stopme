using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StopMe.Core.Models;

namespace StopMe.Infrastructure.Persistence.Configurations;

internal sealed class UsageEventConfiguration : IEntityTypeConfiguration<UsageEvent>
{
    public void Configure(EntityTypeBuilder<UsageEvent> b)
    {
        b.ToTable("UsageEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.DomainHash).HasMaxLength(128);
        b.Property(x => x.AppNameHash).HasMaxLength(128);
        b.Property(x => x.DisplayLabel).HasMaxLength(500);
        b.HasIndex(x => x.OccurredAtUtc);
        b.HasIndex(x => new { x.Type, x.OccurredAtUtc });
        b.HasIndex(x => x.SessionId);
    }
}
