using AuditVitals.Application.Audits;
using FluentValidation;

namespace AuditVitals.Api.Contracts;

public sealed class SubmitAuditRequestValidator : AbstractValidator<SubmitAuditRequestDto>
{
    public SubmitAuditRequestValidator()
    {
        RuleFor(x => x.Url)
            .NotEmpty().WithMessage("URL is required.")
            .MaximumLength(2048).WithMessage("URL must be 2048 characters or fewer.");

        RuleFor(x => x.PageLimit)
            .InclusiveBetween(1, AuditSubmissionService.MaxPageLimitMilestone1)
            .WithMessage($"Page limit must be between 1 and {AuditSubmissionService.MaxPageLimitMilestone1} in this milestone.")
            .When(x => x.PageLimit.HasValue);
    }
}
