using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StopMe.Core.Models;

namespace StopMe.Infrastructure.Persistence.Configurations;

internal sealed class FocusSessionConfiguration : IEntityTypeConfiguration<FocusSession>
{
    public void Configure(EntityTypeBuilder<FocusSession> b)
    {
        b.ToTable("FocusSessions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.Mode).HasConversion<int>();
        b.Property(x => x.LockPolicy).HasConversion<int>();
        b.Property(x => x.RecurrenceRule).HasMaxLength(500);
        b.Property(x => x.TimeZoneId).IsRequired().HasMaxLength(100);
        b.Property(x => x.PasswordHash).HasMaxLength(256);
        b.Property(x => x.RequiredText).HasMaxLength(500);
        b.HasOne(x => x.PausePolicy)
            .WithMany()
            .HasForeignKey(x => x.PausePolicyId)
            .OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.UserProfileId);
        b.HasIndex(x => x.StartAtUtc);
        b.HasIndex(x => new { x.IsRecurring, x.Status });
    }
}
