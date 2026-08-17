using AuditVitals.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuditVitals.Infrastructure.Persistence;

public sealed class AuditVitalsDbContext(DbContextOptions<AuditVitalsDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Audit> Audits => Set<Audit>();
    public DbSet<CrawledPage> CrawledPages => Set<CrawledPage>();
    public DbSet<AuditFinding> AuditFindings => Set<AuditFinding>();
    public DbSet<AuditJob> AuditJobs => Set<AuditJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditVitalsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
