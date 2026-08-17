using AuditVitals.Application.Abstractions;
using AuditVitals.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AuditVitals.Infrastructure.Persistence;

public sealed class EfAuditProcessingUnitOfWork(AuditVitalsDbContext dbContext) : IAuditProcessingUnitOfWork
{
    /// <summary>
    /// Claims a job with a single atomic UPDATE ... RETURNING guarded by
    /// FOR UPDATE SKIP LOCKED: Postgres locks the selected row for the duration
    /// of the update and any concurrent claim attempt skips it instead of
    /// blocking, so two workers can never claim the same job.
    /// </summary>
    public async Task<Guid?> ClaimNextJobIdAsync(string workerId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        var claimed = await dbContext.Database.SqlQuery<Guid>($"""
            UPDATE audit_jobs
            SET status = 'Processing',
                attempt_count = attempt_count + 1,
                locked_at_utc = {nowUtc},
                locked_by = {workerId},
                last_error = NULL,
                updated_at_utc = {nowUtc}
            WHERE id = (
                SELECT id
                FROM audit_jobs
                WHERE status = 'Pending' AND available_at_utc <= {nowUtc}
                ORDER BY available_at_utc
                LIMIT 1
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id
            """)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return claimed.Count == 0 ? null : claimed[0];
    }

    public Task<AuditJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken) =>
        dbContext.AuditJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

    public Task<Audit?> GetAuditAsync(Guid auditId, CancellationToken cancellationToken) =>
        dbContext.Audits.FirstOrDefaultAsync(a => a.Id == auditId, cancellationToken);

    public void AddCrawledPage(CrawledPage page) => dbContext.CrawledPages.Add(page);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
