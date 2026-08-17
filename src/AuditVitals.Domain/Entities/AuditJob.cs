using AuditVitals.Domain.Enums;

namespace AuditVitals.Domain.Entities;

/// <summary>
/// Database-backed queue row driving background processing of an <see cref="Audit"/>.
/// Claiming is performed by the persistence layer via an atomic conditional update
/// (see AuditJobRepository); the methods here only represent the resulting state
/// transitions once a claim, completion, or failure has been decided.
/// </summary>
public sealed class AuditJob
{
    public Guid Id { get; private set; }
    public Guid AuditId { get; private set; }
    public AuditJobStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime AvailableAtUtc { get; private set; }
    public DateTime? LockedAtUtc { get; private set; }
    public string? LockedBy { get; private set; }
    public string? LastError { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private AuditJob()
    {
    }

    public static AuditJob CreateQueued(Guid auditId, DateTime nowUtc)
    {
        return new AuditJob
        {
            Id = Guid.NewGuid(),
            AuditId = auditId,
            Status = AuditJobStatus.Pending,
            AttemptCount = 0,
            AvailableAtUtc = nowUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
        };
    }

    public void MarkClaimed(string lockedBy, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockedBy);
        Status = AuditJobStatus.Processing;
        AttemptCount++;
        LockedAtUtc = nowUtc;
        LockedBy = lockedBy;
        LastError = null;
        UpdatedAtUtc = nowUtc;
    }

    public void MarkCompleted(DateTime nowUtc)
    {
        Status = AuditJobStatus.Completed;
        LockedAtUtc = null;
        LockedBy = null;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Records a failed attempt. Retries by returning to Pending with a backoff
    /// delay when attempts remain, otherwise moves to the terminal Failed state.
    /// </summary>
    public void MarkFailed(string error, DateTime nowUtc, TimeSpan retryDelay, int maxAttempts)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        LastError = error;
        LockedAtUtc = null;
        LockedBy = null;
        UpdatedAtUtc = nowUtc;

        Status = AttemptCount >= maxAttempts ? AuditJobStatus.Failed : AuditJobStatus.Pending;
        AvailableAtUtc = nowUtc.Add(retryDelay);
    }
}
