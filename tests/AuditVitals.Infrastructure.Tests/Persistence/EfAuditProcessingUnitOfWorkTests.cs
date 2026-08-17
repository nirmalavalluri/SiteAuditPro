using AuditVitals.Domain.Entities;
using AuditVitals.Domain.Enums;
using AuditVitals.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuditVitals.Infrastructure.Tests.Persistence;

/// <summary>
/// Exercises the atomic UPDATE ... RETURNING ... FOR UPDATE SKIP LOCKED claim
/// against a real local Postgres instance — this is precisely the mechanism
/// that prevents two workers from ever processing the same audit job, and
/// that guarantee can only be verified against real database locking
/// semantics, not a mock. Requires the local dev Postgres (see docker-compose)
/// running with an "auditvitals_test" database.
/// </summary>
public class EfAuditProcessingUnitOfWorkTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=auditvitals_test;Username=auditvitals;Password=auditvitals_dev_password";

    private static AuditVitalsDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AuditVitalsDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
        return new AuditVitalsDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE audit_findings, crawled_pages, audit_jobs, audits, projects RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<Guid> SeedPendingJobAsync(AuditVitalsDbContext context)
    {
        var nowUtc = DateTime.UtcNow;
        var project = Project.Create("example.com", "https://example.com/", "example.com", null, nowUtc);
        var audit = Audit.CreateQueued(project.Id, "https://example.com/", 25, nowUtc);
        var job = AuditJob.CreateQueued(audit.Id, nowUtc);

        context.Projects.Add(project);
        context.Audits.Add(audit);
        context.AuditJobs.Add(job);
        await context.SaveChangesAsync();

        return job.Id;
    }

    [Fact]
    public async Task ClaimNextJobIdAsync_NoPendingJobs_ReturnsNull()
    {
        await using var context = CreateContext();
        var unitOfWork = new EfAuditProcessingUnitOfWork(context);

        var claimed = await unitOfWork.ClaimNextJobIdAsync("worker-1", DateTime.UtcNow, CancellationToken.None);

        Assert.Null(claimed);
    }

    [Fact]
    public async Task ClaimNextJobIdAsync_ClaimsPendingJobAndSetsProcessingState()
    {
        Guid jobId;
        await using (var seedContext = CreateContext())
        {
            jobId = await SeedPendingJobAsync(seedContext);
        }

        await using var context = CreateContext();
        var unitOfWork = new EfAuditProcessingUnitOfWork(context);

        var claimed = await unitOfWork.ClaimNextJobIdAsync("worker-1", DateTime.UtcNow, CancellationToken.None);

        Assert.Equal(jobId, claimed);

        var job = await unitOfWork.GetJobAsync(jobId, CancellationToken.None);
        Assert.Equal(AuditJobStatus.Processing, job!.Status);
        Assert.Equal("worker-1", job.LockedBy);
        Assert.Equal(1, job.AttemptCount);
    }

    [Fact]
    public async Task ClaimNextJobIdAsync_ConcurrentWorkers_NeverClaimTheSameJobTwice()
    {
        const int jobCount = 8;
        var jobIds = new List<Guid>();
        await using (var seedContext = CreateContext())
        {
            for (var i = 0; i < jobCount; i++)
            {
                jobIds.Add(await SeedPendingJobAsync(seedContext));
            }
        }

        var nowUtc = DateTime.UtcNow;
        var claimTasks = Enumerable.Range(0, jobCount)
            .Select(async i =>
            {
                await using var context = CreateContext();
                var unitOfWork = new EfAuditProcessingUnitOfWork(context);
                return await unitOfWork.ClaimNextJobIdAsync($"worker-{i}", nowUtc, CancellationToken.None);
            });

        var results = await Task.WhenAll(claimTasks);

        var claimed = results.Where(r => r is not null).Select(r => r!.Value).ToList();
        Assert.Equal(jobCount, claimed.Count);
        Assert.Equal(jobCount, claimed.Distinct().Count());
        Assert.Equal(jobIds.OrderBy(x => x), claimed.OrderBy(x => x));
    }
}
