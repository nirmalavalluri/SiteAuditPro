namespace AuditVitals.Application.Parsing;

public sealed record HtmlPageSummary(
    string? Title,
    string? MetaDescription,
    int H1Count,
    int WordCount,
    string? CanonicalUrl,
    bool IsNoIndex);
