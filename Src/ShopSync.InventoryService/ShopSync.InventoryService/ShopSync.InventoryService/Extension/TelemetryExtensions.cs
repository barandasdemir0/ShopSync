using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

namespace ShopSync.InventoryService.Extension;

public static class TelemetryExtensions
{
    public static void AddMonitoring(this WebApplicationBuilder builder)
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
            .WriteTo.Console()//konsola yazdırma işlemi
             .WriteTo.GrafanaLoki(uri: lokiUrl );
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

           


    }

    public static void UseMonitoring(this WebApplication app)
    {
        app.UseSerilogRequestLogging();
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
    }
}
