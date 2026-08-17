using AuditVitals.Application.Crawling;

namespace AuditVitals.Application.Tests.Processing;

internal sealed class FakeSafeHttpFetcher(SafeFetchResult result) : ISafeHttpFetcher
{
    public string? LastRequestedUrl { get; private set; }

    public Task<SafeFetchResult> FetchAsync(string url, CancellationToken cancellationToken)
    {
        LastRequestedUrl = url;
        return Task.FromResult(result);
    }
}
