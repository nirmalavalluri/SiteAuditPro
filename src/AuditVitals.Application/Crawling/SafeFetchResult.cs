namespace AuditVitals.Application.Crawling;

public sealed record SafeFetchResult
{
    public required bool Success { get; init; }
    public string? FailureReason { get; init; }
    public int? StatusCode { get; init; }
    public string? ContentType { get; init; }
    public string? Body { get; init; }
    public long ResponseTimeMilliseconds { get; init; }
    public string? FinalUrl { get; init; }

    public static SafeFetchResult Failed(string reason) => new() { Success = false, FailureReason = reason };

    public static SafeFetchResult Succeeded(int statusCode, string? contentType, string? body, long responseTimeMilliseconds, string finalUrl) =>
        new()
        {
            Success = true,
            StatusCode = statusCode,
            ContentType = contentType,
            Body = body,
            ResponseTimeMilliseconds = responseTimeMilliseconds,
            FinalUrl = finalUrl,
        };
}
