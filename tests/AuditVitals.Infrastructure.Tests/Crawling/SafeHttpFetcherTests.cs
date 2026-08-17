using System.Net;
using System.Text;
using AuditVitals.Application.Security;
using AuditVitals.Infrastructure.Crawling;
using AuditVitals.Infrastructure.Tests.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AuditVitals.Infrastructure.Tests.Crawling;

public class SafeHttpFetcherTests
{
    private static SafeHttpFetcher CreateFetcher(
        FakeHttpMessageHandler handler,
        FakeDnsResolver resolver,
        CrawlerOptions? options = null)
    {
        var httpClient = new HttpClient(handler);
        var validator = new SsrfSafeUrlValidator(resolver);
        return new SafeHttpFetcher(httpClient, validator, Options.Create(options ?? new CrawlerOptions()), NullLogger<SafeHttpFetcher>.Instance);
    }

    [Fact]
    public async Task FetchAsync_SuccessfulResponse_ReturnsBodyAndStatus()
    {
        var resolver = new FakeDnsResolver().WithHost("example.com", "93.184.216.34");
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html><title>Hi</title></html>", Encoding.UTF8, "text/html"),
        });
        var fetcher = CreateFetcher(handler, resolver);

        var result = await fetcher.FetchAsync("https://example.com/", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Contains("Hi", result.Body);
        Assert.Equal("text/html", result.ContentType);
    }

    [Fact]
    public async Task FetchAsync_RedirectToPublicHost_IsFollowedAndFinalResponseReturned()
    {
        var resolver = new FakeDnsResolver()
            .WithHost("old.example.com", "93.184.216.34")
            .WithHost("new.example.com", "93.184.216.35");

        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.Host == "old.example.com")
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.MovedPermanently);
                redirect.Headers.Location = new Uri("https://new.example.com/");
                return redirect;
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("landed", Encoding.UTF8, "text/html"),
            };
        });

        var fetcher = CreateFetcher(handler, resolver);
        var result = await fetcher.FetchAsync("https://old.example.com/", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("landed", result.Body);
        Assert.Equal("https://new.example.com/", result.FinalUrl);
    }

    [Fact]
    public async Task FetchAsync_RedirectToProhibitedAddress_Fails()
    {
        var resolver = new FakeDnsResolver()
            .WithHost("public.example.com", "93.184.216.34")
            .WithHost("internal.example.com", "10.0.0.5");

        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.Host == "public.example.com")
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri("http://internal.example.com/secrets");
                return redirect;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("should not be reached") };
        });

        var fetcher = CreateFetcher(handler, resolver);
        var result = await fetcher.FetchAsync("https://public.example.com/", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("validation failed", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(handler.Requests, r => r.RequestUri!.Host == "internal.example.com");
    }

    [Fact]
    public async Task FetchAsync_ExcessiveRedirects_Fails()
    {
        var resolver = new FakeDnsResolver()
            .WithHost("hop0.example.com", "93.184.216.1")
            .WithHost("hop1.example.com", "93.184.216.2")
            .WithHost("hop2.example.com", "93.184.216.3")
            .WithHost("hop3.example.com", "93.184.216.4");

        var handler = new FakeHttpMessageHandler(request =>
        {
            var host = request.RequestUri!.Host;
            var nextHop = host switch
            {
                "hop0.example.com" => "hop1.example.com",
                "hop1.example.com" => "hop2.example.com",
                "hop2.example.com" => "hop3.example.com",
                _ => null,
            };

            if (nextHop is null)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("final") };
            }

            var redirect = new HttpResponseMessage(HttpStatusCode.Found);
            redirect.Headers.Location = new Uri($"https://{nextHop}/");
            return redirect;
        });

        var options = new CrawlerOptions { MaxRedirects = 2 };
        var fetcher = CreateFetcher(handler, resolver, options);

        var result = await fetcher.FetchAsync("https://hop0.example.com/", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("maximum", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchAsync_RequestTimeout_Fails()
    {
        var resolver = new FakeDnsResolver().WithHost("slow.example.com", "93.184.216.34");
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("too slow") },
            delay: TimeSpan.FromSeconds(2));

        var options = new CrawlerOptions { RequestTimeoutSeconds = 1 };
        var fetcher = CreateFetcher(handler, resolver, options);

        var result = await fetcher.FetchAsync("https://slow.example.com/", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("timed out", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchAsync_CallerCancellation_ThrowsOperationCanceledException()
    {
        var resolver = new FakeDnsResolver().WithHost("example.com", "93.184.216.34");
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") },
            delay: TimeSpan.FromSeconds(5));

        var fetcher = CreateFetcher(handler, resolver);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fetcher.FetchAsync("https://example.com/", cts.Token));
    }

    [Fact]
    public async Task FetchAsync_ResponseExceedsMaxSize_Fails()
    {
        var resolver = new FakeDnsResolver().WithHost("big.example.com", "93.184.216.34");
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('a', 10_000), Encoding.UTF8, "text/html"),
        });

        var options = new CrawlerOptions { MaxResponseBytes = 100 };
        var fetcher = CreateFetcher(handler, resolver, options);

        var result = await fetcher.FetchAsync("https://big.example.com/", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("maximum size", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchAsync_DisallowedContentType_Fails()
    {
        var resolver = new FakeDnsResolver().WithHost("binary.example.com", "93.184.216.34");
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("binary-ish", Encoding.UTF8, "application/octet-stream"),
        });

        var fetcher = CreateFetcher(handler, resolver);
        var result = await fetcher.FetchAsync("https://binary.example.com/", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("not allowed", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FetchAsync_NeverSendsAuthorizationHeader_AcrossRedirect()
    {
        var resolver = new FakeDnsResolver()
            .WithHost("a.example.com", "93.184.216.1")
            .WithHost("b.example.com", "93.184.216.2");

        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.Host == "a.example.com")
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri("https://b.example.com/");
                return redirect;
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") };
        });

        var fetcher = CreateFetcher(handler, resolver);
        await fetcher.FetchAsync("https://a.example.com/", CancellationToken.None);

        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, r => Assert.Null(r.Headers.Authorization));
    }

    [Fact]
    public async Task FetchAsync_InvalidUrl_FailsWithoutSendingRequest()
    {
        var resolver = new FakeDnsResolver();
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var fetcher = CreateFetcher(handler, resolver);
        var result = await fetcher.FetchAsync("http://127.0.0.1/admin", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Empty(handler.Requests);
    }
}
