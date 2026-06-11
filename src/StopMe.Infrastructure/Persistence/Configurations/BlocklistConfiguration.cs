using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StopMe.Core.Models;

namespace StopMe.Infrastructure.Persistence.Configurations;

internal sealed class BlocklistConfiguration : IEntityTypeConfiguration<Blocklist>
{
    public void Configure(EntityTypeBuilder<Blocklist> b)
    {
        b.ToTable("Blocklists");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.Property(x => x.Mode).HasConversion<int>();
        b.HasMany(x => x.Rules)
            .WithOne(x => x.Blocklist)
            .HasForeignKey(x => x.BlocklistId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.UserProfileId);
        b.HasIndex(x => new { x.UserProfileId, x.Name }).IsUnique();
    }
}
