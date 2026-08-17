using AuditVitals.Domain.Entities;

namespace AuditVitals.Domain.Tests.Entities;

public class ProjectTests
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_AllowsNullUserId_ForAnonymousAudits()
    {
        var project = Project.Create("example.com", "https://example.com/", "example.com", userId: null, NowUtc);

        Assert.Null(project.UserId);
        Assert.Equal("example.com", project.NormalizedHost);
        Assert.Equal(NowUtc, project.CreatedAtUtc);
        Assert.Null(project.LastAuditAtUtc);
    }

    [Fact]
    public void RecordAuditStarted_UpdatesLastAuditAtUtc()
    {
        var project = Project.Create("example.com", "https://example.com/", "example.com", userId: null, NowUtc);

        project.RecordAuditStarted(NowUtc.AddDays(1));

        Assert.Equal(NowUtc.AddDays(1), project.LastAuditAtUtc);
    }

    [Theory]
    [InlineData("", "https://example.com/", "example.com")]
    [InlineData("example.com", "", "example.com")]
    [InlineData("example.com", "https://example.com/", "")]
    public void Create_RejectsMissingRequiredFields(string name, string rootUrl, string normalizedHost)
    {
        Assert.Throws<ArgumentException>(() => Project.Create(name, rootUrl, normalizedHost, null, NowUtc));
    }
}
