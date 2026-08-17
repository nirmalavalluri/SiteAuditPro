using AuditVitals.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditVitals.Infrastructure.Persistence.Configurations;

public sealed class AuditJobConfiguration : IEntityTypeConfiguration<AuditJob>
{
    public void Configure(EntityTypeBuilder<AuditJob> builder)
    {
        builder.ToTable("audit_jobs");
        builder.HasKey(j => j.Id);

        builder.Property(j => j.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(j => j.LockedBy).HasMaxLength(200);
        builder.Property(j => j.LastError).HasMaxLength(2000);
        builder.Property(j => j.AvailableAtUtc).IsRequired();
        builder.Property(j => j.CreatedAtUtc).IsRequired();
        builder.Property(j => j.UpdatedAtUtc).IsRequired();

        // Concurrency token: the row-level claim in AuditJobRepository relies on
        // an atomic conditional UPDATE, not this token, but it still guards
        // against lost updates from any other concurrent write path.
        builder.Property<uint>("xmin").IsRowVersion();

        builder.HasOne<Audit>()
            .WithMany()
            .HasForeignKey(j => j.AuditId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(j => j.AuditId).IsUnique();
        builder.HasIndex(j => new { j.Status, j.AvailableAtUtc });
    }
}
