using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.Grafana.Loki;

namespace ShopSync.OrderService.Extension;

public static class TelemetryExtensions
{
    public static WebApplicationBuilder AddMonitoring(this WebApplicationBuilder builder)
    {
        var serviceName = builder.Environment.ApplicationName;
        var lokiUrl = builder.Configuration["Loki:Url"]
           ?? throw new InvalidOperationException("Loki:Url configuration is missing.");
        var otlpEndpoint = builder.Configuration["Otlp:Endpoint"]
            ?? throw new InvalidOperationException("Otlp:Endpoint configuration is missing.");
        // Serilog yapılandırması (InventoryService ile aynı)
        builder.Host.UseSerilog((context, configuration) =>
        {
            configuration.ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ApplicationName", serviceName)
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .WriteTo.File(
                    "Logs/order-service-.txt",
                    rollingInterval: RollingInterval.Day)
            .WriteTo.Console()
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
        // OpenTelemetry yapılandırması
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource =>
            {
                resource.AddService(serviceName);
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                // OrderService gRPC istemci olduğu için HttpClient trace'leri ekleniyor
                // Bu sayede InventoryService'e yapılan gRPC çağrıları Jaeger'da görünür
                .AddHttpClientInstrumentation()
                .AddGrpcClientInstrumentation()
                .AddOtlpExporter(options =>
                {
                    options.Endpoint = new Uri(otlpEndpoint);
                });
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter()
                .AddMeter("ShopSync.OrderService")
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
