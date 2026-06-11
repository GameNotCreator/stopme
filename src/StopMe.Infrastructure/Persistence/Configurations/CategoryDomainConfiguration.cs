using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StopMe.Core.Models;

namespace StopMe.Infrastructure.Persistence.Configurations;

internal sealed class CategoryDomainConfiguration : IEntityTypeConfiguration<CategoryDomain>
{
    public void Configure(EntityTypeBuilder<CategoryDomain> b)
    {
        b.ToTable("CategoryDomains");
        b.HasKey(x => x.Id);
        b.Property(x => x.CategoryName).IsRequired().HasMaxLength(100);
        b.Property(x => x.Domain).IsRequired().HasMaxLength(500);
        b.Property(x => x.Source).IsRequired().HasMaxLength(50);
        b.HasIndex(x => x.CategoryName);
        b.HasIndex(x => new { x.CategoryName, x.Domain }).IsUnique();
    }
}
