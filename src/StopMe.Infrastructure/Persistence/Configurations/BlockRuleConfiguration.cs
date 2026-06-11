using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StopMe.Core.Models;

namespace StopMe.Infrastructure.Persistence.Configurations;

internal sealed class BlockRuleConfiguration : IEntityTypeConfiguration<BlockRule>
{
    public void Configure(EntityTypeBuilder<BlockRule> b)
    {
        b.ToTable("BlockRules");
        b.HasKey(x => x.Id);
        b.Property(x => x.Type).HasConversion<int>();
        b.Property(x => x.Value).IsRequired().HasMaxLength(2000);
        b.Property(x => x.Notes).HasMaxLength(1000);
        b.HasIndex(x => x.BlocklistId);
        b.HasIndex(x => new { x.BlocklistId, x.Type });
    }
}
