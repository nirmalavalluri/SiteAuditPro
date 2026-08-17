using AuditVitals.Application.Processing;
using Microsoft.Extensions.Options;

namespace AuditVitals.Worker;

/// <summary>
/// Polls the database-backed job queue. On each tick it drains every claimable
/// job before sleeping for the configured poll interval, so a burst of queued
/// audits is worked through immediately rather than one per tick.
/// </summary>
public sealed class AuditProcessingBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<AuditWorkerOptions> options,
    ILogger<AuditProcessingBackgroundService> logger)
    : BackgroundService
{
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    private readonly AuditWorkerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("AuditVitals worker {WorkerId} starting", _workerId);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool processedAny;
                do
                {
                    using var scope = scopeFactory.CreateScope();
                    var processingService = scope.ServiceProvider.GetRequiredService<AuditProcessingService>();
                    processedAny = await processingService.ProcessNextAsync(_workerId, stoppingToken).ConfigureAwait(false);
                }
                while (processedAny && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Worker {WorkerId} poll loop failed unexpectedly; will retry after the poll interval", _workerId);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_options.PollIntervalSeconds), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("AuditVitals worker {WorkerId} stopping", _workerId);
    }
}
