using AuditVitals.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuditVitals.Infrastructure.Persistence.Configurations;

public sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
        builder.Property(p => p.RootUrl).IsRequired().HasMaxLength(2048);
        builder.Property(p => p.NormalizedHost).IsRequired().HasMaxLength(255);
        builder.Property(p => p.CreatedAtUtc).IsRequired();

        builder.HasIndex(p => p.NormalizedHost);
        builder.HasIndex(p => p.UserId);
    }
}
