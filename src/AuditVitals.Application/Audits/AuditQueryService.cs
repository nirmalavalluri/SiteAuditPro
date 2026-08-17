using AuditVitals.Application.Abstractions;

namespace AuditVitals.Application.Audits;

public sealed class AuditQueryService(IAuditSubmissionUnitOfWork unitOfWork)
{
    public async Task<AuditStatusResult?> GetAuditStatusAsync(Guid auditId, CancellationToken cancellationToken)
    {
        var audit = await unitOfWork.GetAuditByIdAsync(auditId, cancellationToken).ConfigureAwait(false);
        return audit is null ? null : AuditStatusResult.FromEntity(audit);
    }
}
