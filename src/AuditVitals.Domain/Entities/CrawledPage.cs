namespace AuditVitals.Domain.Entities;

public sealed class CrawledPage
{
    public Guid Id { get; private set; }
    public Guid AuditId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string NormalizedUrl { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public string? ContentType { get; private set; }
    public string? Title { get; private set; }
    public string? MetaDescription { get; private set; }
    public int H1Count { get; private set; }
    public string? CanonicalUrl { get; private set; }
    public int WordCount { get; private set; }
    public long ResponseTimeMilliseconds { get; private set; }
    public bool IsIndexable { get; private set; }
    public int CrawlDepth { get; private set; }
    public string? ContentHash { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private CrawledPage()
    {
    }

    public static CrawledPage Create(Guid auditId, string url, string normalizedUrl, int crawlDepth, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedUrl);
        if (crawlDepth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(crawlDepth), "Crawl depth cannot be negative.");
        }

        return new CrawledPage
        {
            Id = Guid.NewGuid(),
            AuditId = auditId,
            Url = url,
            NormalizedUrl = normalizedUrl,
            CrawlDepth = crawlDepth,
            CreatedAtUtc = nowUtc,
        };
    }

    public void RecordFetchResult(
        int statusCode,
        string? contentType,
        string? title,
        string? metaDescription,
        int h1Count,
        string? canonicalUrl,
        int wordCount,
        long responseTimeMilliseconds,
        bool isIndexable,
        string? contentHash)
    {
        StatusCode = statusCode;
        ContentType = contentType;
        Title = title;
        MetaDescription = metaDescription;
        H1Count = h1Count;
        CanonicalUrl = canonicalUrl;
        WordCount = wordCount;
        ResponseTimeMilliseconds = responseTimeMilliseconds;
        IsIndexable = isIndexable;
        ContentHash = contentHash;
    }
}
