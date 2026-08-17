namespace AuditVitals.Domain.Enums;

/// <summary>
/// Queue lifecycle state for an <see cref="Entities.AuditJob"/>, distinct from
/// the user-facing <see cref="AuditStatus"/> of the audit it drives.
/// </summary>
public enum AuditJobStatus
{
    Pending = 0,
    Processing = 1,
    Completed = 2,
    Failed = 3,
}
