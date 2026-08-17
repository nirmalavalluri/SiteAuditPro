using System.Net;
using System.Net.Http.Json;
using AuditVitals.Api.Contracts;

namespace AuditVitals.Api.IntegrationTests;

public class AuditEndpointsTests : IClassFixture<AuditVitalsApiFactory>, IAsyncLifetime
{
    private readonly HttpClient _client;
    private readonly AuditVitalsApiFactory _factory;

    public AuditEndpointsTests(AuditVitalsApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync() => await _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task PostAudits_ValidPublicUrl_Returns202WithStatusUrl()
    {
        var response = await _client.PostAsJsonAsync("/api/audits", new SubmitAuditRequestDto("https://example.com/", null));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubmitAuditResponseDto>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.AuditId);
        Assert.Equal($"/api/audits/{body.AuditId}", body.StatusUrl);
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://localhost/")]
    [InlineData("ftp://example.com/")]
    [InlineData("http://10.0.0.5/")]
    public async Task PostAudits_ProhibitedOrUnsupportedUrl_Returns400(string url)
    {
        var response = await _client.PostAsJsonAsync("/api/audits", new SubmitAuditRequestDto(url, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAudits_EmptyUrl_Returns400WithValidationError()
    {
        var response = await _client.PostAsJsonAsync("/api/audits", new SubmitAuditRequestDto("", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAudits_PageLimitOutOfRange_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/audits", new SubmitAuditRequestDto("https://example.com/", 500));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAudit_AfterSubmission_ReturnsQueuedStatus()
    {
        var submitResponse = await _client.PostAsJsonAsync("/api/audits", new SubmitAuditRequestDto("https://example.com/", null));
        var submitted = await submitResponse.Content.ReadFromJsonAsync<SubmitAuditResponseDto>();

        var statusResponse = await _client.GetAsync($"/api/audits/{submitted!.AuditId}");

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        var status = await statusResponse.Content.ReadFromJsonAsync<AuditStatusResponseDto>();
        Assert.Equal("Queued", status!.Status);
        Assert.Equal("https://example.com/", status.RequestedUrl);
        Assert.Equal(25, status.RequestedPageLimit);
    }

    [Fact]
    public async Task GetAudit_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/audits/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task HealthLive_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ChecksDatabaseAndReturnsHealthy()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
