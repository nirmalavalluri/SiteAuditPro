using AuditVitals.Application.Abstractions;
using AuditVitals.Domain.Entities;

namespace AuditVitals.Application.Tests.Processing;

internal sealed class FakeAuditProcessingUnitOfWork : IAuditProcessingUnitOfWork
{
    public Dictionary<Guid, AuditJob> Jobs { get; } = [];
    public Dictionary<Guid, Audit> Audits { get; } = [];
    public List<CrawledPage> CrawledPages { get; } = [];
    public int SaveChangesCallCount { get; private set; }

    public Guid? NextClaimableJobId { get; set; }

    public Task<Guid?> ClaimNextJobIdAsync(string workerId, DateTime nowUtc, CancellationToken cancellationToken)
    {
        if (NextClaimableJobId is { } jobId && Jobs.TryGetValue(jobId, out var job))
        {
            job.MarkClaimed(workerId, nowUtc);
        }

        return Task.FromResult(NextClaimableJobId);
    }

    public Task<AuditJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken) =>
        Task.FromResult(Jobs.GetValueOrDefault(jobId));

    public Task<Audit?> GetAuditAsync(Guid auditId, CancellationToken cancellationToken) =>
        Task.FromResult(Audits.GetValueOrDefault(auditId));

    public void AddCrawledPage(CrawledPage page) => CrawledPages.Add(page);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.FromResult(0);
    }
}
