namespace AuditVitals.Application.Audits;

public sealed class SubmitAuditResult
{
    public bool Success { get; }
    public Guid? AuditId { get; }
    public string? ValidationError { get; }

    private SubmitAuditResult(bool success, Guid? auditId, string? validationError)
    {
        Success = success;
        AuditId = auditId;
        ValidationError = validationError;
    }

    public static SubmitAuditResult Succeeded(Guid auditId) => new(true, auditId, null);

    public static SubmitAuditResult ValidationFailed(string error) => new(false, null, error);
}
