using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StopMe.Core.Models;

namespace StopMe.Infrastructure.Persistence.Configurations;

internal sealed class BypassEventConfiguration : IEntityTypeConfiguration<BypassEvent>
{
    public void Configure(EntityTypeBuilder<BypassEvent> b)
    {
        b.ToTable("BypassEvents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.Source).HasMaxLength(200);
        b.Property(x => x.Detail).HasMaxLength(2000);
        b.HasIndex(x => x.DetectedAtUtc);
        b.HasIndex(x => new { x.Type, x.DetectedAtUtc });
        b.HasIndex(x => x.SessionId);
    }
}
