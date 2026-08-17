namespace AuditVitals.Api.Contracts;

public sealed record SubmitAuditResponseDto(Guid AuditId, string StatusUrl);
