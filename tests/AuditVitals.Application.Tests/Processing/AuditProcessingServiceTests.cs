using AuditVitals.Application.Crawling;
using AuditVitals.Application.Processing;
using AuditVitals.Domain.Entities;
using AuditVitals.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AuditVitals.Application.Tests.Processing;

public class AuditProcessingServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static AuditProcessingService CreateService(FakeAuditProcessingUnitOfWork unitOfWork, FakeSafeHttpFetcher fetcher, AuditWorkerOptions? options = null) =>
        new(unitOfWork, fetcher, TimeProvider.System, Options.Create(options ?? new AuditWorkerOptions()), NullLogger<AuditProcessingService>.Instance);

    private static (Audit audit, AuditJob job) SeedQueuedAudit(FakeAuditProcessingUnitOfWork unitOfWork, string url = "https://example.com/")
    {
        var audit = Audit.CreateQueued(Guid.NewGuid(), url, 25, NowUtc);
        var job = AuditJob.CreateQueued(audit.Id, NowUtc);
        unitOfWork.Audits[audit.Id] = audit;
        unitOfWork.Jobs[job.Id] = job;
        unitOfWork.NextClaimableJobId = job.Id;
        return (audit, job);
    }

    [Fact]
    public async Task ProcessNextAsync_NoJobAvailable_ReturnsFalse()
    {
        var unitOfWork = new FakeAuditProcessingUnitOfWork { NextClaimableJobId = null };
        var fetcher = new FakeSafeHttpFetcher(SafeFetchResult.Failed("unused"));
        var service = CreateService(unitOfWork, fetcher);

        var processed = await service.ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.False(processed);
        Assert.Equal(0, unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ProcessNextAsync_SuccessfulFetch_CompletesAuditAndJobAndRecordsPage()
    {
        var unitOfWork = new FakeAuditProcessingUnitOfWork();
        var (audit, job) = SeedQueuedAudit(unitOfWork);
        var fetchResult = SafeFetchResult.Succeeded(200, "text/html", "<html><head><title>Hi</title></head><body>hello world</body></html>", 42, audit.RequestedUrl);
        var fetcher = new FakeSafeHttpFetcher(fetchResult);
        var service = CreateService(unitOfWork, fetcher);

        var processed = await service.ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.True(processed);
        Assert.Equal(AuditStatus.Completed, audit.Status);
        Assert.Equal(AuditJobStatus.Completed, job.Status);
        Assert.Single(unitOfWork.CrawledPages);
        Assert.Equal("Hi", unitOfWork.CrawledPages[0].Title);
        Assert.Equal(200, unitOfWork.CrawledPages[0].StatusCode);
        Assert.True(unitOfWork.SaveChangesCallCount >= 2);
    }

    [Fact]
    public async Task ProcessNextAsync_FailedFetch_MarksAuditAndJobFailed()
    {
        var unitOfWork = new FakeAuditProcessingUnitOfWork();
        var (audit, job) = SeedQueuedAudit(unitOfWork);
        var fetcher = new FakeSafeHttpFetcher(SafeFetchResult.Failed("DNS resolution failed"));
        var service = CreateService(unitOfWork, fetcher, new AuditWorkerOptions { MaxAttempts = 3 });

        var processed = await service.ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.True(processed);
        Assert.Equal(AuditStatus.Failed, audit.Status);
        Assert.Equal("DNS resolution failed", audit.FailureReason);
        // First failed attempt with MaxAttempts=3 should retry, not terminally fail.
        Assert.Equal(AuditJobStatus.Pending, job.Status);
    }

    [Fact]
    public async Task ProcessNextAsync_FailedFetchAtMaxAttempts_TerminatesJob()
    {
        var unitOfWork = new FakeAuditProcessingUnitOfWork();
        var (audit, job) = SeedQueuedAudit(unitOfWork);
        var fetcher = new FakeSafeHttpFetcher(SafeFetchResult.Failed("timeout"));
        var service = CreateService(unitOfWork, fetcher, new AuditWorkerOptions { MaxAttempts = 1 });

        await service.ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.Equal(AuditJobStatus.Failed, job.Status);
    }

    [Fact]
    public async Task ProcessNextAsync_AuditMissing_MarksJobFailedWithoutThrowing()
    {
        var unitOfWork = new FakeAuditProcessingUnitOfWork();
        var job = AuditJob.CreateQueued(Guid.NewGuid(), NowUtc); // AuditId points nowhere
        unitOfWork.Jobs[job.Id] = job;
        unitOfWork.NextClaimableJobId = job.Id;
        var fetcher = new FakeSafeHttpFetcher(SafeFetchResult.Failed("unused"));
        var service = CreateService(unitOfWork, fetcher);

        var processed = await service.ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.True(processed);
        Assert.NotEqual(AuditJobStatus.Processing, job.Status);
    }

    [Fact]
    public async Task ProcessNextAsync_UsesAuditRequestedUrl_ForFetch()
    {
        var unitOfWork = new FakeAuditProcessingUnitOfWork();
        var (audit, _) = SeedQueuedAudit(unitOfWork, "https://another-example.com/");
        var fetcher = new FakeSafeHttpFetcher(SafeFetchResult.Succeeded(200, "text/html", "<html></html>", 10, audit.RequestedUrl));
        var service = CreateService(unitOfWork, fetcher);

        await service.ProcessNextAsync("worker-1", CancellationToken.None);

        Assert.Equal("https://another-example.com/", fetcher.LastRequestedUrl);
    }
}
