using System.Threading.RateLimiting;
using AuditVitals.Api.Contracts;
using AuditVitals.Api.Endpoints;
using AuditVitals.Application;
using AuditVitals.Infrastructure;
using AuditVitals.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddOpenApi();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "AuditVitals API",
            Version = "v1",
            Description = "Technical SEO auditing API. Submit a public website URL and track its audit status.",
        });
    });

    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddValidatorsFromAssemblyContaining<SubmitAuditRequestValidator>();

    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Default", policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            }
        });
    });

    var auditSubmissionPermitLimit = builder.Configuration.GetValue("RateLimiting:AuditSubmission:PermitLimit", 5);
    var auditSubmissionWindowSeconds = builder.Configuration.GetValue("RateLimiting:AuditSubmission:WindowSeconds", 60);

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

        // Public audit submission is unauthenticated and triggers real outbound
        // fetches, so it gets a stricter, IP-scoped limit than the rest of the API.
        options.AddPolicy("audit-submission", context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = auditSubmissionPermitLimit,
                Window = TimeSpan.FromSeconds(auditSubmissionWindowSeconds),
                QueueLimit = 0,
            }));
    });

    builder.Services.AddHealthChecks()
        .AddNpgSql(
            sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("AuditVitals")
                ?? throw new InvalidOperationException("Missing 'ConnectionStrings:AuditVitals' configuration value."),
            name: "postgresql",
            tags: ["ready"]);

    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.UseSwagger();
        app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "AuditVitals API v1"));
    }

    app.UseHttpsRedirection();
    app.UseCors("Default");
    app.UseRateLimiter();

    app.MapAuditEndpoints();

    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false,
    });
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
    });

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "AuditVitals API terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

/// <summary>Entry point marker so WebApplicationFactory can target this assembly in integration tests.</summary>
public partial class Program;
