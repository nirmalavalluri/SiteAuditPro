using AuditVitals.Api.Contracts;
using AuditVitals.Application.Audits;
using FluentValidation;

namespace AuditVitals.Api.Endpoints;

public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audits").WithTags("Audits");

        group.MapPost("/", SubmitAuditAsync)
            .WithName("SubmitAudit")
            .WithSummary("Submit a public website URL for a new audit.")
            .Produces<SubmitAuditResponseDto>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .RequireRateLimiting("audit-submission");

        group.MapGet("/{auditId:guid}", GetAuditStatusAsync)
            .WithName("GetAuditStatus")
            .WithSummary("Get the current status of an audit.")
            .Produces<AuditStatusResponseDto>()
            .Produces(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> SubmitAuditAsync(
        SubmitAuditRequestDto dto,
        IValidator<SubmitAuditRequestDto> validator,
        AuditSubmissionService submissionService,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToValidationProblemErrors());
        }

        var result = await submissionService.SubmitAsync(
            new SubmitAuditRequest(dto.Url, dto.PageLimit, UserId: null),
            cancellationToken);

        if (!result.Success)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["url"] = [result.ValidationError!],
            });
        }

        var statusUrl = $"/api/audits/{result.AuditId}";
        return Results.Accepted(statusUrl, new SubmitAuditResponseDto(result.AuditId!.Value, statusUrl));
    }

    private static async Task<IResult> GetAuditStatusAsync(
        Guid auditId,
        AuditQueryService queryService,
        CancellationToken cancellationToken)
    {
        var status = await queryService.GetAuditStatusAsync(auditId, cancellationToken);
        return status is null ? Results.NotFound() : Results.Ok(AuditStatusResponseDto.FromResult(status));
    }

    private static Dictionary<string, string[]> ToValidationProblemErrors(this FluentValidation.Results.ValidationResult validationResult) =>
        validationResult.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
}
