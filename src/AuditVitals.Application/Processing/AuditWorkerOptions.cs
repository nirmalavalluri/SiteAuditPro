namespace AuditVitals.Application.Processing;

public sealed class AuditWorkerOptions
{
    public const string SectionName = "Worker";

    public int PollIntervalSeconds { get; set; } = 3;

    public int MaxAttempts { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 30;
}
