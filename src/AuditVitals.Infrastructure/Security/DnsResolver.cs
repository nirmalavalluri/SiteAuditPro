using System.Net;
using AuditVitals.Application.Security;

namespace AuditVitals.Infrastructure.Security;

public sealed class DnsResolver : IDnsResolver
{
    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string hostname, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(hostname, cancellationToken).ConfigureAwait(false);
        return addresses;
    }
}
