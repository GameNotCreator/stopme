using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StopMe.Core.Models;

namespace StopMe.Infrastructure.Persistence.Configurations;

internal sealed class SessionBlocklistConfiguration : IEntityTypeConfiguration<SessionBlocklist>
{
    public void Configure(EntityTypeBuilder<SessionBlocklist> b)
    {
        b.ToTable("SessionBlocklists");
        b.HasKey(x => new { x.FocusSessionId, x.BlocklistId });
        b.HasOne(x => x.FocusSession)
            .WithMany(x => x.SessionBlocklists)
            .HasForeignKey(x => x.FocusSessionId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Blocklist)
            .WithMany()
            .HasForeignKey(x => x.BlocklistId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.BlocklistId);
    }
}
