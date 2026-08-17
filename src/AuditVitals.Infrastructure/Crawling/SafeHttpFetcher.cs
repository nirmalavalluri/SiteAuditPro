using System.Diagnostics;
using System.Net;
using System.Text;
using AuditVitals.Application.Crawling;
using AuditVitals.Application.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuditVitals.Infrastructure.Crawling;

/// <summary>
/// SSRF-safe HTTP fetcher. Every URL — the initial request and each redirect
/// target — is re-validated through <see cref="SsrfSafeUrlValidator"/> (which
/// re-resolves DNS) before a request is made, so a host that rebinds to a
/// private address between hops is caught. Redirects are followed manually
/// (the injected <see cref="HttpClient"/> must have AllowAutoRedirect disabled)
/// so each hop can be validated and so no header is ever carried across hosts.
/// </summary>
public sealed class SafeHttpFetcher : ISafeHttpFetcher
{
    private static readonly HttpStatusCode[] RedirectStatusCodes =
    [
        HttpStatusCode.MovedPermanently,
        HttpStatusCode.Found,
        HttpStatusCode.SeeOther,
        HttpStatusCode.TemporaryRedirect,
        HttpStatusCode.PermanentRedirect,
    ];

    private readonly HttpClient _httpClient;
    private readonly SsrfSafeUrlValidator _urlValidator;
    private readonly CrawlerOptions _options;
    private readonly ILogger<SafeHttpFetcher> _logger;

    public SafeHttpFetcher(
        HttpClient httpClient,
        SsrfSafeUrlValidator urlValidator,
        IOptions<CrawlerOptions> options,
        ILogger<SafeHttpFetcher> logger)
    {
        _httpClient = httpClient;
        _urlValidator = urlValidator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SafeFetchResult> FetchAsync(string url, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));

        try
        {
            return await FetchWithRedirectsAsync(url, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return SafeFetchResult.Failed($"Request to {url} timed out after {_options.RequestTimeoutSeconds}s.");
        }
        catch (ResponseTooLargeException ex)
        {
            return SafeFetchResult.Failed(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return SafeFetchResult.Failed($"Request to {url} failed: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    private async Task<SafeFetchResult> FetchWithRedirectsAsync(string url, CancellationToken cancellationToken)
    {
        var currentUrl = url;
        var stopwatch = Stopwatch.StartNew();

        for (var redirectCount = 0; redirectCount <= _options.MaxRedirects; redirectCount++)
        {
            var validation = await _urlValidator.ValidateAsync(currentUrl, cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Refusing to fetch {Url}: {Reason}", currentUrl, validation.FailureMessage);
                return SafeFetchResult.Failed($"URL validation failed: {validation.FailureMessage}");
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, validation.Value!.NormalizedUrl);
            request.Headers.UserAgent.ParseAdd(_options.UserAgent);
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml");

            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (RedirectStatusCodes.Contains(response.StatusCode))
            {
                var location = response.Headers.Location;
                if (location is null)
                {
                    return SafeFetchResult.Failed($"Redirect response from {currentUrl} was missing a Location header.");
                }

                currentUrl = location.IsAbsoluteUri
                    ? location.ToString()
                    : new Uri(new Uri(validation.Value.NormalizedUrl), location).ToString();
                continue;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType;
            if (contentType is not null &&
                _options.AllowedContentTypes.Length > 0 &&
                !_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            {
                return SafeFetchResult.Failed($"Content type '{contentType}' is not allowed.");
            }

            var body = await ReadBoundedAsync(response, _options.MaxResponseBytes, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            return SafeFetchResult.Succeeded(
                (int)response.StatusCode,
                contentType,
                body,
                stopwatch.ElapsedMilliseconds,
                validation.Value.NormalizedUrl);
        }

        return SafeFetchResult.Failed($"Exceeded maximum of {_options.MaxRedirects} redirects fetching {url}.");
    }

    private static async Task<string> ReadBoundedAsync(HttpResponseMessage response, long maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        var readBuffer = new byte[8192];
        long totalRead = 0;

        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalRead += bytesRead;
            if (totalRead > maxBytes)
            {
                throw new ResponseTooLargeException(maxBytes);
            }

            buffer.Write(readBuffer, 0, bytesRead);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
