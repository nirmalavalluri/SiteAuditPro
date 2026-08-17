using System.Net;

namespace AuditVitals.Application.Security;

/// <summary>
/// Validates and normalizes a user-submitted website URL before it is ever used
/// to make a request. Rejects unsupported schemes, embedded credentials, and
/// hosts that resolve to non-public IP ranges. DNS resolution is delegated to
/// <see cref="IDnsResolver"/> so tests do not depend on real DNS, and so the
/// same resolution/classification rules can be reapplied at connect time by the
/// safe HTTP fetcher to guard against DNS rebinding between validation and fetch.
/// </summary>
public sealed class SsrfSafeUrlValidator
{
    private static readonly HashSet<string> ProhibitedHostLiterals = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost",
        "localhost.localdomain",
        "ip6-localhost",
        "ip6-loopback",
        "metadata.google.internal",
        "metadata.goog",
    };

    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase) { "http", "https" };

    private readonly IDnsResolver _dnsResolver;

    public SsrfSafeUrlValidator(IDnsResolver dnsResolver)
    {
        _dnsResolver = dnsResolver ?? throw new ArgumentNullException(nameof(dnsResolver));
    }

    /// <summary>
    /// Throws <see cref="OperationCanceledException"/> if <paramref name="cancellationToken"/>
    /// is cancelled by the caller. DNS-side failures and timeouts are reported as a
    /// failed <see cref="UrlValidationResult"/> instead, since they are expected,
    /// recoverable outcomes rather than caller-initiated cancellation.
    /// </summary>
    public async Task<UrlValidationResult> ValidateAsync(string? rawUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return UrlValidationResult.Failure(UrlValidationFailureReason.InvalidFormat, "URL is required.");
        }

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return UrlValidationResult.Failure(UrlValidationFailureReason.InvalidFormat, "URL is not a well-formed absolute URL.");
        }

        if (!AllowedSchemes.Contains(uri.Scheme))
        {
            return UrlValidationResult.Failure(
                UrlValidationFailureReason.UnsupportedScheme,
                $"Scheme '{uri.Scheme}' is not allowed. Use http or https.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return UrlValidationResult.Failure(
                UrlValidationFailureReason.EmbeddedCredentials,
                "URLs with embedded credentials are not allowed.");
        }

        var host = uri.IdnHost;
        if (string.IsNullOrWhiteSpace(host))
        {
            return UrlValidationResult.Failure(UrlValidationFailureReason.MissingHost, "URL must include a hostname.");
        }

        if (ProhibitedHostLiterals.Contains(host))
        {
            return UrlValidationResult.Failure(UrlValidationFailureReason.ProhibitedHost, $"Host '{host}' is not allowed.");
        }

        IReadOnlyList<IPAddress> addresses;

        if (IPAddress.TryParse(host, out var literalAddress))
        {
            addresses = [literalAddress];
        }
        else
        {
            try
            {
                addresses = await _dnsResolver.ResolveAsync(host, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return UrlValidationResult.Failure(UrlValidationFailureReason.Timeout, $"Timed out resolving host '{host}'.");
            }
            catch (TimeoutException ex)
            {
                return UrlValidationResult.Failure(UrlValidationFailureReason.Timeout, $"Timed out resolving host '{host}': {ex.Message}");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return UrlValidationResult.Failure(UrlValidationFailureReason.DnsResolutionFailed, $"Could not resolve host '{host}': {ex.Message}");
            }
        }

        if (addresses.Count == 0)
        {
            return UrlValidationResult.Failure(UrlValidationFailureReason.DnsResolutionFailed, $"Host '{host}' did not resolve to any address.");
        }

        foreach (var address in addresses)
        {
            if (IpAddressSafetyClassifier.IsProhibited(address, out var reason))
            {
                return UrlValidationResult.Failure(
                    UrlValidationFailureReason.ProhibitedIpAddress,
                    $"Host '{host}' resolves to a disallowed address: {reason}");
            }
        }

        var normalizedUrl = Normalize(uri, host);
        return UrlValidationResult.Success(new ValidatedUrl(rawUrl, normalizedUrl, host, addresses));
    }

    private static string Normalize(Uri uri, string host)
    {
        var builder = new UriBuilder(uri)
        {
            Host = host,
            Fragment = string.Empty,
        };

        if (uri.IsDefaultPort)
        {
            builder.Port = -1;
        }

        return builder.Uri.ToString();
    }
}
