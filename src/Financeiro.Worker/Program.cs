using Azure.Messaging.ServiceBus;
using Financeiro.Worker;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using OpenTelemetry.Metrics;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// 1. Configurar o Kestrel para responder na porta 8080 do contentor
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8080);
});

// 2. Configurações de Conexão
var serviceBusConnection = builder.Configuration.GetConnectionString("AzureServiceBus");
var clientOptions = new ServiceBusClientOptions
{
    TransportType = ServiceBusTransportType.AmqpTcp
};

builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection, clientOptions));

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var redisConn = configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(redisConn);
});

// 3. Registo das Métricas do OpenTelemetry
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics
            .AddMeter(WorkerMetrics.MeterName)
            .AddRuntimeInstrumentation()
            .AddPrometheusExporter();
    });

// 4. Registo do Worker em segundo plano
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

// 5. Expor o endpoint /metrics para recolha pelo Prometheus
app.UseOpenTelemetryPrometheusScrapingEndpoint();

await app.RunAsync();