using AuditVitals.Domain.Entities;

namespace AuditVitals.Application.Abstractions;

public interface IAuditProcessingUnitOfWork
{
    /// <summary>
    /// Atomically claims the next available job (Pending, due, not locked by
    /// another worker) and returns its id, or null if none is available.
    /// Must be implemented as a single atomic statement so two workers can
    /// never claim the same job.
    /// </summary>
    Task<Guid?> ClaimNextJobIdAsync(string workerId, DateTime nowUtc, CancellationToken cancellationToken);

    Task<AuditJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken);

    Task<Audit?> GetAuditAsync(Guid auditId, CancellationToken cancellationToken);

    void AddCrawledPage(CrawledPage page);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
