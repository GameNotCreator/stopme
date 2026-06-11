using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StopMe.Core.Models;

namespace StopMe.Infrastructure.Persistence.Configurations;

internal sealed class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> b)
    {
        b.ToTable("Devices");
        b.HasKey(x => x.Id);
        b.Property(x => x.DeviceName).IsRequired().HasMaxLength(200);
        b.Property(x => x.Platform).IsRequired().HasMaxLength(32);
        b.Property(x => x.MachineIdHash).IsRequired().HasMaxLength(128);
        b.HasIndex(x => x.UserProfileId);
        b.HasIndex(x => x.MachineIdHash);
    }
}
