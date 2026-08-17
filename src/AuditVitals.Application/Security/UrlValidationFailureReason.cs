namespace AuditVitals.Application.Security;

public enum UrlValidationFailureReason
{
    InvalidFormat,
    UnsupportedScheme,
    MissingHost,
    EmbeddedCredentials,
    ProhibitedHost,
    DnsResolutionFailed,
    ProhibitedIpAddress,
    Cancelled,
    Timeout,
}
