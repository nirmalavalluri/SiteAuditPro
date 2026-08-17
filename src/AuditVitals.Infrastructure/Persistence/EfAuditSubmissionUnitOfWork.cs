using AuditVitals.Application.Abstractions;
using AuditVitals.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuditVitals.Infrastructure.Persistence;

public sealed class EfAuditSubmissionUnitOfWork(AuditVitalsDbContext dbContext) : IAuditSubmissionUnitOfWork
{
    public Task<Project?> FindProjectByHostAsync(string normalizedHost, Guid? userId, CancellationToken cancellationToken) =>
        dbContext.Projects.FirstOrDefaultAsync(p => p.NormalizedHost == normalizedHost && p.UserId == userId, cancellationToken);

    public void AddProject(Project project) => dbContext.Projects.Add(project);

    public void AddAudit(Audit audit) => dbContext.Audits.Add(audit);

    public void AddAuditJob(AuditJob job) => dbContext.AuditJobs.Add(job);

    public Task<Audit?> GetAuditByIdAsync(Guid auditId, CancellationToken cancellationToken) =>
        dbContext.Audits.FirstOrDefaultAsync(a => a.Id == auditId, cancellationToken);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
