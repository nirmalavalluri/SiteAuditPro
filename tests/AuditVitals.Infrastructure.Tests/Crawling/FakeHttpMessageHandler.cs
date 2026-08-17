namespace AuditVitals.Infrastructure.Tests.Crawling;

/// <summary>In-memory <see cref="HttpMessageHandler"/> test double — no real network involved.</summary>
internal sealed class FakeHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responder,
    TimeSpan? delay = null) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);

        if (delay is { } d)
        {
            await Task.Delay(d, cancellationToken).ConfigureAwait(false);
        }

        return responder(request);
    }
}
