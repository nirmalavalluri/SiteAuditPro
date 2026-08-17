namespace AuditVitals.Application.Crawling;

/// <summary>
/// Performs a single bounded, SSRF-safe HTTP GET. Implementations must validate
/// the URL (and every redirect target) before connecting, cap response size and
/// duration, and limit the number of redirects followed.
/// </summary>
public interface ISafeHttpFetcher
{
    Task<SafeFetchResult> FetchAsync(string url, CancellationToken cancellationToken);
}
