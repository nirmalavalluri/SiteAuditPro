using AuditVitals.Application.Abstractions;
using AuditVitals.Application.Security;
using AuditVitals.Domain.Entities;

namespace AuditVitals.Application.Audits;

/// <summary>
/// Validates a submitted URL, finds-or-creates its owning project, and queues a
/// new audit with an accompanying job row, all committed by one SaveChanges call.
/// </summary>
public sealed class AuditSubmissionService(
    SsrfSafeUrlValidator urlValidator,
    IAuditSubmissionUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    /// <summary>
    /// No subscription/billing model exists yet (Milestone 1), so every audit is
    /// capped at the Free-tier page limit from the product's planned pricing.
    /// </summary>
    public const int DefaultPageLimit = 25;
    public const int MaxPageLimitMilestone1 = 25;

    public async Task<SubmitAuditResult> SubmitAsync(SubmitAuditRequest request, CancellationToken cancellationToken)
    {
        var validation = await urlValidator.ValidateAsync(request.Url, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return SubmitAuditResult.ValidationFailed(validation.FailureMessage ?? "The provided URL is not allowed.");
        }

        var validated = validation.Value!;
        var nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        var project = await unitOfWork.FindProjectByHostAsync(validated.Host, request.UserId, cancellationToken).ConfigureAwait(false);
        if (project is null)
        {
            project = Project.Create(validated.Host, validated.NormalizedUrl, validated.Host, request.UserId, nowUtc);
            unitOfWork.AddProject(project);
        }

        var pageLimit = Math.Clamp(request.PageLimit ?? DefaultPageLimit, 1, MaxPageLimitMilestone1);

        var audit = Audit.CreateQueued(project.Id, validated.NormalizedUrl, pageLimit, nowUtc);
        unitOfWork.AddAudit(audit);

        var job = AuditJob.CreateQueued(audit.Id, nowUtc);
        unitOfWork.AddAuditJob(job);

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return SubmitAuditResult.Succeeded(audit.Id);
    }
}
