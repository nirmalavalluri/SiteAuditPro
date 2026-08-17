using AuditVitals.Domain.Entities;

namespace AuditVitals.Domain.Tests.Entities;

public class CrawledPageTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_SetsRequiredFields()
    {
        var page = CrawledPage.Create(Guid.NewGuid(), "https://example.com/", "https://example.com", crawlDepth: 0, NowUtc);

        Assert.Equal("https://example.com/", page.Url);
        Assert.Equal(0, page.CrawlDepth);
        Assert.Equal(NowUtc, page.CreatedAtUtc);
    }

    [Fact]
    public void Create_RejectsNegativeCrawlDepth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CrawledPage.Create(Guid.NewGuid(), "https://example.com/", "https://example.com", crawlDepth: -1, NowUtc));
    }

    [Fact]
    public void RecordFetchResult_PopulatesResponseFields()
    {
        var page = CrawledPage.Create(Guid.NewGuid(), "https://example.com/", "https://example.com", 0, NowUtc);

        page.RecordFetchResult(
            statusCode: 200,
            contentType: "text/html",
            title: "Example",
            metaDescription: "An example page",
            h1Count: 1,
            canonicalUrl: "https://example.com/",
            wordCount: 500,
            responseTimeMilliseconds: 120,
            isIndexable: true,
            contentHash: "abc123");

        Assert.Equal(200, page.StatusCode);
        Assert.Equal("Example", page.Title);
        Assert.Equal(1, page.H1Count);
        Assert.True(page.IsIndexable);
        Assert.Equal(120, page.ResponseTimeMilliseconds);
    }
}
