using AuditVitals.Domain.Entities;
using AuditVitals.Domain.Enums;

namespace AuditVitals.Application.Audits;

public sealed record AuditStatusResult(
    Guid AuditId,
    AuditStatus Status,
    string RequestedUrl,
    int RequestedPageLimit,
    int CrawledPageCount,
    int DiscoveredPageCount,
    int? HealthScore,
    bool IsSampledAudit,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? FailedAtUtc,
    string? FailureReason)
{
    public static AuditStatusResult FromEntity(Audit audit) => new(
        audit.Id,
        audit.Status,
        audit.RequestedUrl,
        audit.RequestedPageLimit,
        audit.CrawledPageCount,
        audit.DiscoveredPageCount,
        audit.HealthScore,
        audit.IsSampledAudit,
        audit.CreatedAtUtc,
        audit.StartedAtUtc,
        audit.CompletedAtUtc,
        audit.FailedAtUtc,
        audit.FailureReason);
}
