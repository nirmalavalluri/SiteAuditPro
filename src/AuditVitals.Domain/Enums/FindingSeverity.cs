namespace AuditVitals.Domain.Enums;

/// <summary>
/// Ordered ascending by importance so callers can compare severities numerically.
/// Numeric values are fixed explicitly because they are persisted.
/// </summary>
public enum FindingSeverity
{
    Informational = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}
