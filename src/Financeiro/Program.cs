using Financeiro.Domain.Interfaces.Services;
using Financeiro.Services;
using Microsoft.Extensions.Azure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. Injeção de Dependência dos Clientes Azure (Service Bus e Azurite Blob)
builder.Services.AddAzureClients(clientBuilder =>
{
    // Registra o BlobServiceClient apontando para a connection string do Azurite
    clientBuilder.AddBlobServiceClient(
        builder.Configuration.GetConnectionString("AzureBlobStorage")
    );

    // Registra o ServiceBusClient apontando para o Service Bus Emulator
    clientBuilder.AddServiceBusClient(
        builder.Configuration.GetConnectionString("AzureServiceBus")
    );
});

// injeção de serviços : 

builder.Services.AddScoped<ICsvStreamParserService, CsvStreamParserService>();

builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
