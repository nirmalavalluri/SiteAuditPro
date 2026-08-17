using AuditVitals.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditVitals.Infrastructure.Persistence.Configurations;

public sealed class CrawledPageConfiguration : IEntityTypeConfiguration<CrawledPage>
{
    public void Configure(EntityTypeBuilder<CrawledPage> builder)
    {
        builder.ToTable("crawled_pages");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Url).IsRequired().HasMaxLength(2048);
        builder.Property(p => p.NormalizedUrl).IsRequired().HasMaxLength(2048);
        builder.Property(p => p.ContentType).HasMaxLength(200);
        builder.Property(p => p.Title).HasMaxLength(500);
        builder.Property(p => p.MetaDescription).HasMaxLength(1000);
        builder.Property(p => p.H1Count).HasColumnName("h1_count");
        builder.Property(p => p.CanonicalUrl).HasMaxLength(2048);
        builder.Property(p => p.ContentHash).HasMaxLength(64);
        builder.Property(p => p.CreatedAtUtc).IsRequired();

        builder.HasOne<Audit>()
            .WithMany()
            .HasForeignKey(p => p.AuditId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => p.AuditId);
        builder.HasIndex(p => new { p.AuditId, p.NormalizedUrl }).IsUnique();
    }
}
