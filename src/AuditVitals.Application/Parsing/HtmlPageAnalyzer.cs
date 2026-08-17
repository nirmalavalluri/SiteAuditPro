using AngleSharp.Html.Parser;

namespace AuditVitals.Application.Parsing;

/// <summary>
/// Extracts the small set of on-page facts Milestone 1's worker records for a
/// fetched page. Pure and synchronous — no I/O — so it needs no abstraction to
/// be unit tested. The technical SEO rules engine that judges these facts
/// (missing title, thin content, etc.) is a later milestone.
/// </summary>
public static class HtmlPageAnalyzer
{
    private static readonly HtmlParser Parser = new();

    public static HtmlPageSummary Analyze(string html)
    {
        var document = Parser.ParseDocument(html);

        var title = document.Title?.Trim();
        var metaDescription = document
            .QuerySelector("meta[name='description' i]")
            ?.GetAttribute("content")
            ?.Trim();
        var canonicalUrl = document
            .QuerySelector("link[rel='canonical' i]")
            ?.GetAttribute("href")
            ?.Trim();
        var robotsContent = document.QuerySelector("meta[name='robots' i]")?.GetAttribute("content") ?? string.Empty;

        var h1Count = document.QuerySelectorAll("h1").Length;
        var wordCount = document.Body?.TextContent
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Length ?? 0;

        return new HtmlPageSummary(
            string.IsNullOrWhiteSpace(title) ? null : title,
            string.IsNullOrWhiteSpace(metaDescription) ? null : metaDescription,
            h1Count,
            wordCount,
            string.IsNullOrWhiteSpace(canonicalUrl) ? null : canonicalUrl,
            robotsContent.Contains("noindex", StringComparison.OrdinalIgnoreCase));
    }
}
