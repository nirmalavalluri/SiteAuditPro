using AuditVitals.Application.Audits;

namespace AuditVitals.Api.Contracts;

public sealed record AuditStatusResponseDto(
    Guid AuditId,
    string Status,
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
    public static AuditStatusResponseDto FromResult(AuditStatusResult result) => new(
        result.AuditId,
        result.Status.ToString(),
        result.RequestedUrl,
        result.RequestedPageLimit,
        result.CrawledPageCount,
        result.DiscoveredPageCount,
        result.HealthScore,
        result.IsSampledAudit,
        result.CreatedAtUtc,
        result.StartedAtUtc,
        result.CompletedAtUtc,
        result.FailedAtUtc,
        result.FailureReason);
}
