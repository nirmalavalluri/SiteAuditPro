namespace AuditVitals.Infrastructure.Crawling;

internal sealed class ResponseTooLargeException(long maxBytes) : Exception($"Response exceeded maximum size of {maxBytes} bytes.")
{
}
