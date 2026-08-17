using System.Net;
using System.Net.Sockets;
using AuditVitals.Application.Security;

namespace AuditVitals.Application.Tests.Security;

/// <summary>Configurable <see cref="IDnsResolver"/> test double — no real DNS involved.</summary>
internal sealed class FakeDnsResolver : IDnsResolver
{
    private readonly Dictionary<string, IReadOnlyList<IPAddress>> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private Exception? _exceptionToThrow;
    private TimeSpan? _delay;

    public FakeDnsResolver WithHost(string host, params string[] addresses)
    {
        _hosts[host] = addresses.Select(IPAddress.Parse).ToArray();
        return this;
    }

    public FakeDnsResolver ThrowsOnResolve(Exception exception)
    {
        _exceptionToThrow = exception;
        return this;
    }

    /// <summary>Simulates slow DNS; the delay respects the caller's cancellation token.</summary>
    public FakeDnsResolver WithDelay(TimeSpan delay)
    {
        _delay = delay;
        return this;
    }

    public async Task<IReadOnlyList<IPAddress>> ResolveAsync(string hostname, CancellationToken cancellationToken)
    {
        if (_delay is { } delay)
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        if (_exceptionToThrow is not null)
        {
            throw _exceptionToThrow;
        }

        if (_hosts.TryGetValue(hostname, out var addresses))
        {
            return addresses;
        }

        throw new SocketException((int)SocketError.HostNotFound);
    }
}
