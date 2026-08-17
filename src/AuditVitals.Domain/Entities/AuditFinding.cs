using AuditVitals.Domain.Enums;

namespace AuditVitals.Domain.Entities;

public sealed class AuditFinding
{
    public Guid Id { get; private set; }
    public Guid AuditId { get; private set; }

    /// <summary>Null for site-wide findings that are not tied to a single page.</summary>
    public Guid? CrawledPageId { get; private set; }

    public string RuleCode { get; private set; } = string.Empty;
    public FindingCategory Category { get; private set; }
    public FindingSeverity Severity { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string WhyItMatters { get; private set; } = string.Empty;
    public string RecommendedFix { get; private set; } = string.Empty;
    public string? Evidence { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private AuditFinding()
    {
    }

    public static AuditFinding Create(
        Guid auditId,
        Guid? crawledPageId,
        string ruleCode,
        FindingCategory category,
        FindingSeverity severity,
        string title,
        string description,
        string whyItMatters,
        string recommendedFix,
        string? evidence,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(whyItMatters);
        ArgumentException.ThrowIfNullOrWhiteSpace(recommendedFix);

        return new AuditFinding
        {
            Id = Guid.NewGuid(),
            AuditId = auditId,
            CrawledPageId = crawledPageId,
            RuleCode = ruleCode,
            Category = category,
            Severity = severity,
            Title = title,
            Description = description,
            WhyItMatters = whyItMatters,
            RecommendedFix = recommendedFix,
            Evidence = evidence,
            CreatedAtUtc = nowUtc,
        };
    }
}
