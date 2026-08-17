namespace AuditVitals.Application.Audits;

public sealed record SubmitAuditRequest(string Url, int? PageLimit, Guid? UserId);
