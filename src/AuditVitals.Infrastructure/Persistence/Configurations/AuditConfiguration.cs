using AuditVitals.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditVitals.Infrastructure.Persistence.Configurations;

public sealed class AuditConfiguration : IEntityTypeConfiguration<Audit>
{
    public void Configure(EntityTypeBuilder<Audit> builder)
    {
        builder.ToTable("audits");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.RequestedUrl).IsRequired().HasMaxLength(2048);
        builder.Property(a => a.FailureReason).HasMaxLength(2000);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(a => a.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => a.ProjectId);
        builder.HasIndex(a => a.Status);
    }
}
