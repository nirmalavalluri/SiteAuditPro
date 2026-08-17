using AuditVitals.Domain.Entities;
using AuditVitals.Domain.Enums;

namespace AuditVitals.Domain.Tests.Entities;

public class AuditJobTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateQueued_SetsPendingStatusAndZeroAttempts()
    {
        var job = AuditJob.CreateQueued(Guid.NewGuid(), NowUtc);

        Assert.Equal(AuditJobStatus.Pending, job.Status);
        Assert.Equal(0, job.AttemptCount);
        Assert.Equal(NowUtc, job.AvailableAtUtc);
    }

    [Fact]
    public void MarkClaimed_IncrementsAttemptCountAndSetsLock()
    {
        var job = AuditJob.CreateQueued(Guid.NewGuid(), NowUtc);

        job.MarkClaimed("worker-1", NowUtc.AddSeconds(1));

        Assert.Equal(AuditJobStatus.Processing, job.Status);
        Assert.Equal(1, job.AttemptCount);
        Assert.Equal("worker-1", job.LockedBy);
        Assert.Equal(NowUtc.AddSeconds(1), job.LockedAtUtc);
    }

    [Fact]
    public void MarkCompleted_ClearsLockAndSetsCompleted()
    {
        var job = AuditJob.CreateQueued(Guid.NewGuid(), NowUtc);
        job.MarkClaimed("worker-1", NowUtc);

        job.MarkCompleted(NowUtc.AddSeconds(2));

        Assert.Equal(AuditJobStatus.Completed, job.Status);
        Assert.Null(job.LockedBy);
        Assert.Null(job.LockedAtUtc);
    }

    [Fact]
    public void MarkFailed_BelowMaxAttempts_ReturnsToPendingWithBackoff()
    {
        var job = AuditJob.CreateQueued(Guid.NewGuid(), NowUtc);
        job.MarkClaimed("worker-1", NowUtc);

        job.MarkFailed("timeout", NowUtc.AddSeconds(5), TimeSpan.FromMinutes(1), maxAttempts: 3);

        Assert.Equal(AuditJobStatus.Pending, job.Status);
        Assert.Equal("timeout", job.LastError);
        Assert.Null(job.LockedBy);
        Assert.Equal(NowUtc.AddSeconds(5).AddMinutes(1), job.AvailableAtUtc);
    }

    [Fact]
    public void MarkFailed_AtMaxAttempts_TransitionsToTerminalFailed()
    {
        var job = AuditJob.CreateQueued(Guid.NewGuid(), NowUtc);
        job.MarkClaimed("worker-1", NowUtc);
        job.MarkFailed("timeout", NowUtc, TimeSpan.FromMinutes(1), maxAttempts: 1);

        Assert.Equal(AuditJobStatus.Failed, job.Status);
    }

    [Fact]
    public void MarkClaimed_RejectsEmptyLockedBy()
    {
        var job = AuditJob.CreateQueued(Guid.NewGuid(), NowUtc);

        Assert.Throws<ArgumentException>(() => job.MarkClaimed(string.Empty, NowUtc));
    }
}
