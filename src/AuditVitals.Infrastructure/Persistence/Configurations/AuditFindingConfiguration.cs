using AuditVitals.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditVitals.Infrastructure.Persistence.Configurations;

public sealed class AuditFindingConfiguration : IEntityTypeConfiguration<AuditFinding>
{
    public void Configure(EntityTypeBuilder<AuditFinding> builder)
    {
        builder.ToTable("audit_findings");
        builder.HasKey(f => f.Id);

        builder.Property(f => f.RuleCode).IsRequired().HasMaxLength(100);
        builder.Property(f => f.Title).IsRequired().HasMaxLength(300);
        builder.Property(f => f.Description).IsRequired().HasMaxLength(4000);
        builder.Property(f => f.WhyItMatters).IsRequired().HasMaxLength(2000);
        builder.Property(f => f.RecommendedFix).IsRequired().HasMaxLength(2000);
        builder.Property(f => f.Evidence).HasMaxLength(4000);
        builder.Property(f => f.CreatedAtUtc).IsRequired();
        builder.Property(f => f.Category).HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(f => f.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<Audit>()
            .WithMany()
            .HasForeignKey(f => f.AuditId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<CrawledPage>()
            .WithMany()
            .HasForeignKey(f => f.CrawledPageId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(f => f.AuditId);
        builder.HasIndex(f => f.CrawledPageId);
        builder.HasIndex(f => f.RuleCode);
        builder.HasIndex(f => f.Severity);
    }
}
