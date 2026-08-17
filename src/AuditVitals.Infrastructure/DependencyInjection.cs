using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using AuditVitals.Application.Abstractions;
using AuditVitals.Application.Crawling;
using AuditVitals.Application.Security;
using AuditVitals.Infrastructure.Crawling;
using AuditVitals.Infrastructure.Persistence;
using AuditVitals.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuditVitals.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AuditVitals")
            ?? throw new InvalidOperationException("Missing 'ConnectionStrings:AuditVitals' configuration value.");

        services.AddDbContext<AuditVitalsDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddOptions<CrawlerOptions>().Bind(configuration.GetSection(CrawlerOptions.SectionName));

        services.AddSingleton<IDnsResolver, DnsResolver>();
        services.AddSingleton<SsrfSafeUrlValidator>();
        services.AddScoped<IAuditSubmissionUnitOfWork, EfAuditSubmissionUnitOfWork>();
        services.AddScoped<IAuditProcessingUnitOfWork, EfAuditProcessingUnitOfWork>();

        services.AddHttpClient<ISafeHttpFetcher, SafeHttpFetcher>()
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var dnsResolver = sp.GetRequiredService<IDnsResolver>();
                var handler = new SocketsHttpHandler
                {
                    // Redirects are followed manually by SafeHttpFetcher so each hop
                    // can be re-validated; the built-in redirect handling must stay off.
                    AllowAutoRedirect = false,
                };

                // ConnectCallback pins the TCP connection to a freshly re-resolved,
                // re-validated IP to close the DNS-rebinding window between
                // validating a URL and connecting to it. That only makes sense for
                // a direct connection to the origin: when the deployment mandates an
                // egress proxy (e.g. HTTPS_PROXY), SocketsHttpHandler's first hop is
                // the proxy itself, so ConnectCallback would see the proxy's address
                // instead of the origin's and incorrectly reject it. In that case the
                // proxy is the trusted network boundary, and origin validation still
                // happens up front via SsrfSafeUrlValidator on every request and redirect.
                if (HttpClient.DefaultProxy.GetProxy(new Uri("https://example.org/")) is null)
                {
                    handler.ConnectCallback = (context, cancellationToken) =>
                        ConnectValidatedAsync(context, dnsResolver, cancellationToken);
                }

                return handler;
            })
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        return services;
    }

    /// <summary>
    /// Re-resolves and re-validates the target host at the moment of the actual
    /// TCP connect, using the same DNS resolver and IP classifier as the
    /// submission-time validator. This closes the DNS-rebinding window between
    /// validating a URL and connecting to it, and applies on every redirect hop
    /// since each one triggers a fresh connection.
    /// </summary>
    private static async ValueTask<Stream> ConnectValidatedAsync(
        SocketsHttpConnectionContext context,
        IDnsResolver dnsResolver,
        CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;

        IReadOnlyList<IPAddress> addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await dnsResolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false);

        if (addresses.Count == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        foreach (var address in addresses)
        {
            if (IpAddressSafetyClassifier.IsProhibited(address, out var reason))
            {
                throw new InvalidOperationException($"Refusing to connect to {host} ({address}): {reason}");
            }
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses[0], context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
