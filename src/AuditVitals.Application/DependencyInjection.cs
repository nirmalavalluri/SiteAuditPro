using AuditVitals.Application.Audits;
using AuditVitals.Application.Processing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AuditVitals.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditSubmissionService>();
        services.AddScoped<AuditQueryService>();
        services.AddScoped<AuditProcessingService>();
        services.AddOptions<AuditWorkerOptions>().Bind(configuration.GetSection(AuditWorkerOptions.SectionName));
        return services;
    }
}
