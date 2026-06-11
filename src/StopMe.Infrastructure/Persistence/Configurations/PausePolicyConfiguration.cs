using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StopMe.Core.Models;

namespace StopMe.Infrastructure.Persistence.Configurations;

internal sealed class PausePolicyConfiguration : IEntityTypeConfiguration<PausePolicy>
{
    public void Configure(EntityTypeBuilder<PausePolicy> b)
    {
        b.ToTable("PausePolicies");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<int>();
    }
}
