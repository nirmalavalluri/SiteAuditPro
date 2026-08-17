using AuditVitals.Domain.Entities;

namespace AuditVitals.Application.Abstractions;

/// <summary>
/// Persistence seam for submitting a new audit. A single <see cref="SaveChangesAsync"/>
/// call commits the (possibly new) project, the queued audit, and its audit job
/// together, which is what makes the submission transactional.
/// </summary>
public interface IAuditSubmissionUnitOfWork
{
    Task<Project?> FindProjectByHostAsync(string normalizedHost, Guid? userId, CancellationToken cancellationToken);

    void AddProject(Project project);

    void AddAudit(Audit audit);

    void AddAuditJob(AuditJob job);

    Task<Audit?> GetAuditByIdAsync(Guid auditId, CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
