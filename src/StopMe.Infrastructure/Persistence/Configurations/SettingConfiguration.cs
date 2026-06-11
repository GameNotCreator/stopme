using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StopMe.Core.Models;

namespace StopMe.Infrastructure.Persistence.Configurations;

internal sealed class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> b)
    {
        b.ToTable("Settings");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(200);
        b.Property(x => x.Value).IsRequired();
    }
}
