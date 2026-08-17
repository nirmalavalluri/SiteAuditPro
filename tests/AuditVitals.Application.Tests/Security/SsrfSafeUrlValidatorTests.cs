using System.Net.Sockets;
using AuditVitals.Application.Security;

namespace AuditVitals.Application.Tests.Security;

public class SsrfSafeUrlValidatorTests
{
    [Fact]
    public async Task ValidateAsync_ValidPublicHttpUrl_Succeeds()
    {
        var resolver = new FakeDnsResolver().WithHost("example.com", "93.184.216.34");
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync("http://example.com/", CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("http://example.com/", result.Value!.NormalizedUrl);
    }

    [Fact]
    public async Task ValidateAsync_ValidPublicHttpsUrl_Succeeds()
    {
        var resolver = new FakeDnsResolver().WithHost("example.com", "93.184.216.34");
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync("https://example.com/", CancellationToken.None);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ValidateAsync_UppercaseHostname_NormalizesToLowercase()
    {
        var resolver = new FakeDnsResolver().WithHost("example.com", "93.184.216.34");
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync("HTTP://EXAMPLE.COM/Path", CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("example.com", result.Value!.Host);
        Assert.StartsWith("http://example.com/", result.Value.NormalizedUrl, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://example.com:443/", "https://example.com/")]
    [InlineData("http://example.com:80/", "http://example.com/")]
    public async Task ValidateAsync_DefaultPort_IsStrippedFromNormalizedUrl(string input, string expected)
    {
        var resolver = new FakeDnsResolver().WithHost("example.com", "93.184.216.34");
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync(input, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(expected, result.Value!.NormalizedUrl);
    }

    [Fact]
    public async Task ValidateAsync_NonDefaultPort_IsPreserved()
    {
        var resolver = new FakeDnsResolver().WithHost("example.com", "93.184.216.34");
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync("https://example.com:8443/", CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("https://example.com:8443/", result.Value!.NormalizedUrl);
    }

    [Fact]
    public async Task ValidateAsync_Fragment_IsRemoved()
    {
        var resolver = new FakeDnsResolver().WithHost("example.com", "93.184.216.34");
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync("https://example.com/page#section-2", CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("https://example.com/page", result.Value!.NormalizedUrl);
    }

    [Fact]
    public async Task ValidateAsync_EmbeddedCredentials_Fails()
    {
        var resolver = new FakeDnsResolver().WithHost("example.com", "93.184.216.34");
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync("https://user:pass@example.com/", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.EmbeddedCredentials, result.FailureReason);
    }

    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///etc/passwd")]
    [InlineData("gopher://example.com")]
    [InlineData("javascript:alert(1)")]
    public async Task ValidateAsync_UnsupportedScheme_Fails(string url)
    {
        var validator = new SsrfSafeUrlValidator(new FakeDnsResolver());

        var result = await validator.ValidateAsync(url, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.UnsupportedScheme, result.FailureReason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("http://")]
    public async Task ValidateAsync_MissingOrMalformedHostname_Fails(string url)
    {
        var validator = new SsrfSafeUrlValidator(new FakeDnsResolver());

        var result = await validator.ValidateAsync(url, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.True(
            result.FailureReason is UrlValidationFailureReason.InvalidFormat or UrlValidationFailureReason.MissingHost);
    }

    [Theory]
    [InlineData("http://localhost/")]
    [InlineData("http://LOCALHOST/")]
    [InlineData("http://localhost.localdomain/")]
    [InlineData("http://metadata.google.internal/computeMetadata/v1/")]
    public async Task ValidateAsync_ProhibitedHostLiteral_Fails(string url)
    {
        var validator = new SsrfSafeUrlValidator(new FakeDnsResolver());

        var result = await validator.ValidateAsync(url, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.ProhibitedHost, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_LoopbackIpLiteral_Fails()
    {
        var validator = new SsrfSafeUrlValidator(new FakeDnsResolver());

        var result = await validator.ValidateAsync("http://127.0.0.1/", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.ProhibitedIpAddress, result.FailureReason);
    }

    [Theory]
    [InlineData("http://10.0.0.5/")]
    [InlineData("http://172.16.0.5/")]
    [InlineData("http://192.168.1.5/")]
    [InlineData("http://100.64.0.5/")] // carrier-grade NAT
    public async Task ValidateAsync_IPv4PrivateRanges_Fail(string url)
    {
        var validator = new SsrfSafeUrlValidator(new FakeDnsResolver());

        var result = await validator.ValidateAsync(url, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.ProhibitedIpAddress, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_IPv4LinkLocalRange_Fails()
    {
        var validator = new SsrfSafeUrlValidator(new FakeDnsResolver());

        var result = await validator.ValidateAsync("http://169.254.1.1/", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.ProhibitedIpAddress, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_CloudMetadataAddress_Fails()
    {
        var validator = new SsrfSafeUrlValidator(new FakeDnsResolver());

        var result = await validator.ValidateAsync("http://169.254.169.254/latest/meta-data/", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.ProhibitedIpAddress, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_IPv6Loopback_Fails()
    {
        var validator = new SsrfSafeUrlValidator(new FakeDnsResolver());

        var result = await validator.ValidateAsync("http://[::1]/", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.ProhibitedIpAddress, result.FailureReason);
    }

    [Theory]
    [InlineData("http://[fc00::1]/")] // unique local / private
    [InlineData("http://[fe80::1]/")] // link-local
    [InlineData("http://[fd00:ec2::254]/")] // AWS IMDSv6, falls in fc00::/7
    public async Task ValidateAsync_IPv6PrivateOrLinkLocal_Fails(string url)
    {
        var validator = new SsrfSafeUrlValidator(new FakeDnsResolver());

        var result = await validator.ValidateAsync(url, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.ProhibitedIpAddress, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_IPv4MappedIPv6LoopbackBypass_Fails()
    {
        var validator = new SsrfSafeUrlValidator(new FakeDnsResolver());

        var result = await validator.ValidateAsync("http://[::ffff:127.0.0.1]/", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.ProhibitedIpAddress, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_HostnameResolvingToPrivateAddress_Fails()
    {
        var resolver = new FakeDnsResolver().WithHost("internal.example.com", "10.0.0.5");
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync("http://internal.example.com/", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.ProhibitedIpAddress, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_MultipleDnsResultsWithOneProhibited_Fails()
    {
        var resolver = new FakeDnsResolver().WithHost("multi.example.com", "93.184.216.34", "10.0.0.5");
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync("http://multi.example.com/", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.ProhibitedIpAddress, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_InternationalizedDomainName_ResolvesAndNormalizesToPunycode()
    {
        var resolver = new FakeDnsResolver().WithHost("xn--mnchen-3ya.example", "93.184.216.34");
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync("https://münchen.example/", CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal("xn--mnchen-3ya.example", result.Value!.Host);
    }

    [Fact]
    public async Task ValidateAsync_CallerCancellation_ThrowsOperationCanceledException()
    {
        var resolver = new FakeDnsResolver().WithDelay(TimeSpan.FromSeconds(5)).WithHost("slow.example.com", "93.184.216.34");
        var validator = new SsrfSafeUrlValidator(resolver);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            validator.ValidateAsync("http://slow.example.com/", cts.Token));
    }

    [Fact]
    public async Task ValidateAsync_DnsResolutionFailure_ReturnsDnsResolutionFailed()
    {
        var validator = new SsrfSafeUrlValidator(new FakeDnsResolver());

        var result = await validator.ValidateAsync("http://does-not-exist.invalid/", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.DnsResolutionFailed, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_DnsTimeout_ReturnsTimeoutFailure()
    {
        var resolver = new FakeDnsResolver().ThrowsOnResolve(new TimeoutException("DNS server did not respond"));
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync("http://timeout.example.com/", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.Timeout, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_ResolverInternalCancellation_ReturnsTimeoutFailure()
    {
        // Simulates a resolver with its own internal timeout token distinct from the caller's token.
        var resolver = new FakeDnsResolver().ThrowsOnResolve(new OperationCanceledException("internal DNS timeout"));
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync("http://internal-timeout.example.com/", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.Timeout, result.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_SocketExceptionFromResolver_ReturnsDnsResolutionFailed()
    {
        var resolver = new FakeDnsResolver().ThrowsOnResolve(new SocketException((int)SocketError.HostNotFound));
        var validator = new SsrfSafeUrlValidator(resolver);

        var result = await validator.ValidateAsync("http://unresolvable.example.com/", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(UrlValidationFailureReason.DnsResolutionFailed, result.FailureReason);
    }
}
