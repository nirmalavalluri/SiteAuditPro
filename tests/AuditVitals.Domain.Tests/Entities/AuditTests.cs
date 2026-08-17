using AuditVitals.Domain.Entities;
using AuditVitals.Domain.Enums;

namespace AuditVitals.Domain.Tests.Entities;

public class AuditTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateQueued_SetsQueuedStatus()
    {
        var audit = Audit.CreateQueued(Guid.NewGuid(), "https://example.com/", 25, NowUtc);

        Assert.Equal(AuditStatus.Queued, audit.Status);
        Assert.Equal(25, audit.RequestedPageLimit);
        Assert.Equal(NowUtc, audit.CreatedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CreateQueued_RejectsNonPositivePageLimit(int limit)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Audit.CreateQueued(Guid.NewGuid(), "https://example.com/", limit, NowUtc));
    }

    [Fact]
    public void MarkRunning_FromQueued_TransitionsToRunning()
    {
        var audit = Audit.CreateQueued(Guid.NewGuid(), "https://example.com/", 25, NowUtc);

        audit.MarkRunning(NowUtc.AddSeconds(1));

        Assert.Equal(AuditStatus.Running, audit.Status);
        Assert.Equal(NowUtc.AddSeconds(1), audit.StartedAtUtc);
    }

    [Fact]
    public void MarkRunning_WhenNotQueued_Throws()
    {
        var audit = Audit.CreateQueued(Guid.NewGuid(), "https://example.com/", 25, NowUtc);
        audit.MarkRunning(NowUtc);

        Assert.Throws<InvalidOperationException>(() => audit.MarkRunning(NowUtc));
    }

    [Fact]
    public void MarkCompleted_FromRunning_SetsResultFields()
    {
        var audit = Audit.CreateQueued(Guid.NewGuid(), "https://example.com/", 25, NowUtc);
        audit.MarkRunning(NowUtc);

        audit.MarkCompleted(crawledPageCount: 10, discoveredPageCount: 12, isSampledAudit: true, healthScore: 82, NowUtc.AddMinutes(2));

        Assert.Equal(AuditStatus.Completed, audit.Status);
        Assert.Equal(10, audit.CrawledPageCount);
        Assert.Equal(12, audit.DiscoveredPageCount);
        Assert.True(audit.IsSampledAudit);
        Assert.Equal(82, audit.HealthScore);
        Assert.Equal(NowUtc.AddMinutes(2), audit.CompletedAtUtc);
    }

    [Fact]
    public void MarkCompleted_WhenNotRunning_Throws()
    {
        var audit = Audit.CreateQueued(Guid.NewGuid(), "https://example.com/", 25, NowUtc);

        Assert.Throws<InvalidOperationException>(() =>
            audit.MarkCompleted(1, 1, false, 100, NowUtc));
    }

    [Fact]
    public void MarkFailed_SetsFailureReasonAndTimestamp()
    {
        var audit = Audit.CreateQueued(Guid.NewGuid(), "https://example.com/", 25, NowUtc);
        audit.MarkRunning(NowUtc);

        audit.MarkFailed("DNS resolution failed", NowUtc.AddSeconds(30));

        Assert.Equal(AuditStatus.Failed, audit.Status);
        Assert.Equal("DNS resolution failed", audit.FailureReason);
        Assert.Equal(NowUtc.AddSeconds(30), audit.FailedAtUtc);
    }

    [Fact]
    public void MarkFailed_RejectsEmptyReason()
    {
        var audit = Audit.CreateQueued(Guid.NewGuid(), "https://example.com/", 25, NowUtc);

        Assert.Throws<ArgumentException>(() => audit.MarkFailed(string.Empty, NowUtc));
    }

    [Fact]
    public void MarkCancelled_SetsCancelledStatus()
    {
        var audit = Audit.CreateQueued(Guid.NewGuid(), "https://example.com/", 25, NowUtc);

        audit.MarkCancelled(NowUtc.AddSeconds(5));

        Assert.Equal(AuditStatus.Cancelled, audit.Status);
    }
}
