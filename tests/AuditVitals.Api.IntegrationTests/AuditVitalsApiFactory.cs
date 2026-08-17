using AuditVitals.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuditVitals.Api.IntegrationTests;

/// <summary>
/// Runs the real API pipeline (DI wiring, middleware, SSRF validation, EF Core)
/// against a dedicated local Postgres database, with rate limiting relaxed so
/// a test class making several requests in a row does not trip it.
/// </summary>
public sealed class AuditVitalsApiFactory : WebApplicationFactory<Program>
{
    private const string TestConnectionString =
        "Host=localhost;Port=5432;Database=auditvitals_test;Username=auditvitals;Password=auditvitals_dev_password";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:AuditVitals", TestConnectionString);
        builder.UseSetting("RateLimiting:AuditSubmission:PermitLimit", "1000");
    }

    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuditVitalsDbContext>();
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE audit_findings, crawled_pages, audit_jobs, audits, projects RESTART IDENTITY CASCADE;");
    }
}
