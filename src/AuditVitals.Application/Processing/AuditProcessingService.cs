using AuditVitals.Application.Abstractions;
using AuditVitals.Application.Crawling;
using AuditVitals.Application.Parsing;
using AuditVitals.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditVitals.Application.Processing;

/// <summary>
/// Drives one unit of background audit work: claim a job, run the audit, record
/// the outcome. Milestone 1 fetches only the homepage; later milestones extend
/// this into a full multi-page crawl behind the same claim/record contract.
/// </summary>
public sealed class AuditProcessingService(
    IAuditProcessingUnitOfWork unitOfWork,
    ISafeHttpFetcher fetcher,
    TimeProvider timeProvider,
    IOptions<AuditWorkerOptions> options,
    ILogger<AuditProcessingService> logger)
{
    private readonly AuditWorkerOptions _options = options.Value;

    /// <returns>True if a job was claimed and processed (regardless of outcome), false if none was available.</returns>
    public async Task<bool> ProcessNextAsync(string workerId, CancellationToken cancellationToken)
    {
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;
        var jobId = await unitOfWork.ClaimNextJobIdAsync(workerId, nowUtc, cancellationToken).ConfigureAwait(false);
        if (jobId is null)
        {
            return false;
        }

        var job = await unitOfWork.GetJobAsync(jobId.Value, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Claimed audit job {jobId} could not be reloaded.");

        var audit = await unitOfWork.GetAuditAsync(job.AuditId, cancellationToken).ConfigureAwait(false);
        if (audit is null)
        {
            logger.LogError("Audit job {JobId} references missing audit {AuditId}", job.Id, job.AuditId);
            FailJob(job, "Audit record not found.", nowUtc);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        try
        {
            audit.MarkRunning(nowUtc);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            var fetchResult = await fetcher.FetchAsync(audit.RequestedUrl, cancellationToken).ConfigureAwait(false);
            var finishedAtUtc = timeProvider.GetUtcNow().UtcDateTime;

            if (!fetchResult.Success)
            {
                var reason = fetchResult.FailureReason ?? "Fetch failed for an unknown reason.";
                audit.MarkFailed(reason, finishedAtUtc);
                FailJob(job, reason, finishedAtUtc);
            }
            else
            {
                RecordSuccessfulFetch(audit, job, fetchResult, finishedAtUtc);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unhandled error processing audit job {JobId}", job.Id);
            var failedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            audit.MarkFailed($"Unexpected error: {ex.Message}", failedAtUtc);
            FailJob(job, ex.Message, failedAtUtc);
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private void RecordSuccessfulFetch(Audit audit, AuditJob job, SafeFetchResult fetchResult, DateTime finishedAtUtc)
    {
        var summary = HtmlPageAnalyzer.Analyze(fetchResult.Body ?? string.Empty);
        var isSuccessStatus = fetchResult.StatusCode is >= 200 and < 300;

        var page = CrawledPage.Create(
            audit.Id,
            audit.RequestedUrl,
            fetchResult.FinalUrl ?? audit.RequestedUrl,
            crawlDepth: 0,
            finishedAtUtc);

        page.RecordFetchResult(
            statusCode: fetchResult.StatusCode ?? 0,
            contentType: fetchResult.ContentType,
            title: summary.Title,
            metaDescription: summary.MetaDescription,
            h1Count: summary.H1Count,
            canonicalUrl: summary.CanonicalUrl,
            wordCount: summary.WordCount,
            responseTimeMilliseconds: fetchResult.ResponseTimeMilliseconds,
            isIndexable: isSuccessStatus && !summary.IsNoIndex,
            contentHash: null);

        unitOfWork.AddCrawledPage(page);

        audit.MarkCompleted(
            crawledPageCount: 1,
            discoveredPageCount: 1,
            isSampledAudit: true,
            healthScore: null,
            finishedAtUtc);

        job.MarkCompleted(finishedAtUtc);
    }

    private void FailJob(AuditJob job, string reason, DateTime nowUtc) =>
        job.MarkFailed(reason, nowUtc, TimeSpan.FromSeconds(_options.RetryDelaySeconds), _options.MaxAttempts);
}
