using System.Net;

namespace AuditVitals.Application.Security;

/// <summary>
/// Resolves a hostname to IP addresses. Abstracted so SSRF validation and safe
/// fetching can be unit tested without depending on real DNS infrastructure,
/// and so both the submission-time validator and the connect-time guard share
/// one resolution path.
/// </summary>
public interface IDnsResolver
{
    Task<IReadOnlyList<IPAddress>> ResolveAsync(string hostname, CancellationToken cancellationToken);
}
