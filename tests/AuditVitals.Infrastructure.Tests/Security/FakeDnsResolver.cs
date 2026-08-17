using System.Net;
using AuditVitals.Application.Security;

namespace AuditVitals.Infrastructure.Tests.Security;

/// <summary>Configurable <see cref="IDnsResolver"/> test double — no real DNS involved.</summary>
internal sealed class FakeDnsResolver : IDnsResolver
{
    private readonly Dictionary<string, IReadOnlyList<IPAddress>> _hosts = new(StringComparer.OrdinalIgnoreCase);

    public FakeDnsResolver WithHost(string host, params string[] addresses)
    {
        _hosts[host] = addresses.Select(IPAddress.Parse).ToArray();
        return this;
    }

    public Task<IReadOnlyList<IPAddress>> ResolveAsync(string hostname, CancellationToken cancellationToken)
    {
        if (_hosts.TryGetValue(hostname, out var addresses))
        {
            return Task.FromResult(addresses);
        }

        throw new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.HostNotFound);
    }
}
