namespace AuditVitals.Infrastructure.Crawling;

public sealed class CrawlerOptions
{
    public const string SectionName = "Crawler";

    public int RequestTimeoutSeconds { get; set; } = 15;

    public int MaxRedirects { get; set; } = 5;

    public long MaxResponseBytes { get; set; } = 5 * 1024 * 1024;

    public string UserAgent { get; set; } = "AuditVitalsBot/1.0 (+https://auditvitals.com/bot)";

    public string[] AllowedContentTypes { get; set; } =
    [
        "text/html",
        "application/xhtml+xml",
    ];
}
