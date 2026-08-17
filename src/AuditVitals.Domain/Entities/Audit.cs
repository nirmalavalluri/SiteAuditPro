using AuditVitals.Domain.Enums;

namespace AuditVitals.Domain.Entities;

public sealed class Audit
{
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public AuditStatus Status { get; private set; }
    public string RequestedUrl { get; private set; } = string.Empty;
    public DateTime? StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public DateTime? FailedAtUtc { get; private set; }
    public string? FailureReason { get; private set; }
    public int RequestedPageLimit { get; private set; }
    public int CrawledPageCount { get; private set; }
    public int DiscoveredPageCount { get; private set; }
    public int? HealthScore { get; private set; }
    public bool IsSampledAudit { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private Audit()
    {
    }

    public static Audit CreateQueued(Guid projectId, string requestedUrl, int requestedPageLimit, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedUrl);
        if (requestedPageLimit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requestedPageLimit), "Page limit must be positive.");
        }

        return new Audit
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = AuditStatus.Queued,
            RequestedUrl = requestedUrl,
            RequestedPageLimit = requestedPageLimit,
            CreatedAtUtc = nowUtc,
        };
    }

    public void MarkRunning(DateTime nowUtc)
    {
        EnsureStatus(AuditStatus.Queued);
        Status = AuditStatus.Running;
        StartedAtUtc = nowUtc;
    }

    public void MarkCompleted(int crawledPageCount, int discoveredPageCount, bool isSampledAudit, int? healthScore, DateTime nowUtc)
    {
        EnsureStatus(AuditStatus.Running);
        Status = AuditStatus.Completed;
        CrawledPageCount = crawledPageCount;
        DiscoveredPageCount = discoveredPageCount;
        IsSampledAudit = isSampledAudit;
        HealthScore = healthScore;
        CompletedAtUtc = nowUtc;
    }

    public void MarkFailed(string failureReason, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);
        Status = AuditStatus.Failed;
        FailureReason = failureReason;
        FailedAtUtc = nowUtc;
    }

    public void MarkCancelled(DateTime nowUtc)
    {
        Status = AuditStatus.Cancelled;
        FailedAtUtc = nowUtc;
    }

    private void EnsureStatus(AuditStatus expected)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"Audit {Id} must be {expected} but was {Status}.");
        }
    }
}
