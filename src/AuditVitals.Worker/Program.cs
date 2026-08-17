using AuditVitals.Application;
using AuditVitals.Infrastructure;
using AuditVitals.Worker;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.Services.AddApplication(builder.Configuration);
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddHostedService<AuditProcessingBackgroundService>();

    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AuditVitals worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
