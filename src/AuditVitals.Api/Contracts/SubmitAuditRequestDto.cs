namespace AuditVitals.Api.Contracts;

public sealed record SubmitAuditRequestDto(string Url, int? PageLimit);
