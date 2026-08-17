using System.Net;

namespace AuditVitals.Application.Security;

/// <summary>
/// A URL that has passed SSRF validation: syntactically well-formed, using an
/// allowed scheme, with no resolved address in a prohibited range.
/// </summary>
public sealed record ValidatedUrl(
    string OriginalInput,
    string NormalizedUrl,
    string Host,
    IReadOnlyList<IPAddress> ResolvedAddresses);

public sealed class UrlValidationResult
{
    public bool IsValid { get; }
    public ValidatedUrl? Value { get; }
    public UrlValidationFailureReason? FailureReason { get; }
    public string? FailureMessage { get; }

    private UrlValidationResult(bool isValid, ValidatedUrl? value, UrlValidationFailureReason? failureReason, string? failureMessage)
    {
        IsValid = isValid;
        Value = value;
        FailureReason = failureReason;
        FailureMessage = failureMessage;
    }

    public static UrlValidationResult Success(ValidatedUrl value) => new(true, value, null, null);

    public static UrlValidationResult Failure(UrlValidationFailureReason reason, string message) =>
        new(false, null, reason, message);
}
