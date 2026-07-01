using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

namespace ShopSync.InventoryService.Extension;

public static class TelemetryExtensions
{
    public static WebApplicationBuilder AddMonitoring(this WebApplicationBuilder builder)
    {
        var serviceName = builder.Environment.ApplicationName;

        var lokiUrl = builder.Configuration["Loki:Url"]
           ?? throw new InvalidOperationException("Loki:Url configuration is missing.");

        var otlpEndpoint = builder.Configuration["Otlp:Endpoint"]
            ?? throw new InvalidOperationException("Otlp:Endpoint configuration is missing.");

        builder.Host.UseSerilog((context, configuration) =>
        {
            configuration.ReadFrom.Configuration(context.Configuration) //appsettings.json dosyasındaki log ayarlarını okuma işlemi
            .Enrich.FromLogContext() //loglara context ekleme işlemi
            .Enrich.WithProperty("ApplicationName", serviceName) //uygulama adını loglara ekleme işlemi
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .WriteTo.File(
                    "Logs/inventory-service-.txt",
                    rollingInterval: RollingInterval.Day)
            .WriteTo.Console()//konsola yazdırma işlemi
             .WriteTo.GrafanaLoki(uri: lokiUrl,
             labels:
             [
                 new LokiLabel
                 {
                     Key = "application",
                     Value = serviceName
                 },
                 new LokiLabel
                 {
                     Key = "environment",
                     Value = builder.Environment.EnvironmentName
                 }

                 ]);
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(serviceName);
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                .AddGrpcClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                });
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                });
            });



        return builder;
    }

    public static WebApplication UseMonitoring(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        app.UseOpenTelemetryPrometheusScrapingEndpoint();

        return app;
    }
}
