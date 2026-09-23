using System.Data;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Financeiro.Domain.DTOs;
using Financeiro.Domain.Models;
using Microsoft.Data.SqlClient;
using StackExchange.Redis;

namespace Financeiro.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly ServiceBusClient _serviceBusClient;
    private readonly IDatabase _redis;
    private ServiceBusProcessor? _processor;

    public Worker(ILogger<Worker> logger, IConfiguration configuration, ServiceBusClient serviceBusClient, IConnectionMultiplexer redisMultiplexer)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceBusClient = serviceBusClient;
        _redis = redisMultiplexer.GetDatabase();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var queueName = _configuration["ServiceBusSettings:ImportacaoQueue"] ?? "importacao-lotes-queue";

        _processor = _serviceBusClient.CreateProcessor(queueName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages = false,
            MaxConcurrentCalls = 2
        });

        _processor.ProcessMessageAsync += ProcessarMensagemLoteAsync;
        _processor.ProcessErrorAsync += TratarErroProcessamentoAsync;

        _logger.LogInformation("Iniciando escuta na fila: {QueueName}...", queueName);
        await _processor.StartProcessingAsync(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessarMensagemLoteAsync(ProcessMessageEventArgs args)
    {
        // 1. Log imediato para comprovar recebimento da fila
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n==========================================");
        Console.WriteLine($">>> CHEGOU MENSAGEM NA FILA! ID: {args.Message.MessageId}");
        Console.WriteLine($"==========================================\n");
        Console.ResetColor();

        var corpo = args.Message.Body.ToString();

        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var lote = JsonSerializer.Deserialize<MensagemLoteImportacao>(corpo, options);

            if (lote == null || lote.Transacoes == null || lote.Transacoes.Count == 0)
            {
                _logger.LogWarning("Mensagem vazia ou sem transações no corpo. Concluindo mensagem.");
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            // Verificação de Idempotência via Redis
            string chaveIdempotencia = $"lote_processado:{args.Message.MessageId}";
            bool ehMensagemInedita = await _redis.StringSetAsync(chaveIdempotencia, "PROCESSADO", TimeSpan.FromHours(24), When.NotExists);

            if (!ehMensagemInedita)
            {
                _logger.LogWarning("Mensagem duplicada detectada no Redis: {MessageId}. Ignorando processamento repetido.", args.Message.MessageId);
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            _logger.LogInformation("Processando Lote {Lote} do Protocolo {Protocolo} ({Qtd} itens)...",
                lote.NumeroLote, lote.ProtocoloId, lote.Transacoes.Count);

            await PersistirLoteNoBancoAsync(lote.ProtocoloId, lote.Transacoes);

            await args.CompleteMessageAsync(args.Message);
            _logger.LogInformation("Lote {Lote} concluído e conciliado com sucesso.", lote.NumeroLote);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar mensagem {MessageId}.", args.Message.MessageId);

            string chaveIdempotencia = $"lote_processado:{args.Message.MessageId}";
            await _redis.KeyDeleteAsync(chaveIdempotencia);

            await args.AbandonMessageAsync(args.Message);
        }
    }

    private async Task PersistirLoteNoBancoAsync(Guid protocoloId, List<TransacaoFinanceira> transacoes)
    {
        var connectionString = _configuration.GetConnectionString("SqlDatabase");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        // 1. Inserção em massa com TVP via Stored Procedure
        await using (var cmdInsert = new SqlCommand("ssp_InserirLoteTransacoesFinanceiras", connection))
        {
            cmdInsert.CommandType = CommandType.StoredProcedure;
            cmdInsert.Parameters.Add(new SqlParameter("@Transacoes", SqlDbType.Structured)
            {
                TypeName = "dbo.TransacaoFinanceiraType",
                Value = SqlBatchHelper.ToTable(transacoes)
            });

            await cmdInsert.ExecuteNonQueryAsync();
        }

        // 2. Executa o motor de reconciliação de negócio no banco
        await using (var cmdConciliar = new SqlCommand("ssp_ConciliarTransacoesProtocolo", connection))
        {
            cmdConciliar.CommandType = CommandType.StoredProcedure;
            cmdConciliar.Parameters.Add(new SqlParameter("@ProtocoloImportacaoId", SqlDbType.UniqueIdentifier)
            {
                Value = protocoloId
            });

            await cmdConciliar.ExecuteNonQueryAsync();
        }
    }

    private Task TratarErroProcessamentoAsync(ProcessErrorEventArgs args)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"\n>>> ERRO NO SERVICE BUS: {args.Exception.Message}");
        Console.WriteLine($"Origem do erro: {args.ErrorSource}\n");
        Console.ResetColor();

        _logger.LogError(args.Exception, "Erro no ServiceBus: {Source}", args.ErrorSource);
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}