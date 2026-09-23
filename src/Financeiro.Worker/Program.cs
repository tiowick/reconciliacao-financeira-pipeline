using Azure.Messaging.ServiceBus;
using Financeiro.Worker;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

var serviceBusConnection = builder.Configuration.GetConnectionString("AzureServiceBus");

var clientOptions = new ServiceBusClientOptions
{
    TransportType = ServiceBusTransportType.AmqpTcp,
    //ServerCertificateCustomValidationCallback = (args) => true
};

builder.Services.AddSingleton(new ServiceBusClient(serviceBusConnection, clientOptions));

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var redisConn = configuration.GetConnectionString("Redis") ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(redisConn);
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();