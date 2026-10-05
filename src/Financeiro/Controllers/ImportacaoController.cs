using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using Financeiro.Domain.DTOs;
using Financeiro.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace Financeiro.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ImportacaoController : ControllerBase
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusSender _serviceBusSender;
        private readonly ICsvStreamParserService _csvParser;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImportacaoController> _logger;
        private readonly string _connectionString;

        public ImportacaoController(
            BlobServiceClient blobServiceClient,
            ServiceBusClient serviceBusClient,
            ICsvStreamParserService csvParser,
            IConfiguration configuration,
            ILogger<ImportacaoController> logger)
        {
            _blobServiceClient = blobServiceClient;
            _serviceBusClient = serviceBusClient;
            _csvParser = csvParser;
            _configuration = configuration;
            _logger = logger;

            var queueName = _configuration["ServiceBusSettings:ImportacaoQueue"] ?? "importacao-lotes-queue";
            _serviceBusSender = _serviceBusClient.CreateSender(queueName);

            _connectionString = _configuration.GetConnectionString("SqlDatabase")
                ?? throw new InvalidOperationException("A connection string 'SqlDatabase' não foi encontrada.");
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> UploadArquivo(IFormFile arquivo, CancellationToken cancellationToken)
        {
            if (arquivo == null || arquivo.Length == 0)
                return BadRequest(new { erro = "Arquivo inválido ou vazio." });

            if (!arquivo.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { erro = "Apenas arquivos .csv são suportados no momento." });

            var protocoloId = Guid.NewGuid();
            var nomeBlob = $"{protocoloId}/{arquivo.FileName}";

            try
            {
                // 1. Streaming e upload do arquivo diretamente para o Blob Storage (Azurite)
                var containerName = _configuration["BlobStorageSettings:ContainerName"] ?? "arquivos-importacao";
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                var blobClient = containerClient.GetBlobClient(nomeBlob);
                await using (var uploadStream = arquivo.OpenReadStream())
                {
                    await blobClient.UploadAsync(uploadStream, overwrite: true, cancellationToken);
                }

                _logger.LogInformation("Arquivo {Arquivo} salvo no Blob Storage com sucesso. Protocolo: {ProtocoloId}", arquivo.FileName, protocoloId);

                // 2. Leitura em lotes via streaming e publicação assíncrona no Azure Service Bus
                await using (var parseStream = arquivo.OpenReadStream())
                {
                    int numeroLote = 1;

                    await foreach (var lote in _csvParser.LerEmLotesAsync(parseStream, protocoloId, tamanhoLote: 100, cancellationToken))
                    {
                        var mensagemDto = new MensagemLoteImportacao
                        {
                            ProtocoloId = protocoloId,
                            NumeroLote = numeroLote++,
                            NomeArquivoOriginal = arquivo.FileName,
                            BlobUri = blobClient.Uri.ToString(),
                            Transacoes = lote,
                            DataPublicacao = DateTime.UtcNow
                        };

                        var corpoJson = JsonSerializer.Serialize(mensagemDto);
                        var serviceBusMessage = new ServiceBusMessage(corpoJson)
                        {
                            ContentType = "application/json",
                            MessageId = $"{protocoloId}_{mensagemDto.NumeroLote}",
                            Subject = "LoteTransacoesImportacao"
                        };

                        await _serviceBusSender.SendMessageAsync(serviceBusMessage, cancellationToken);
                    }

                    _logger.LogInformation("Protocolo {ProtocoloId} fatiado e postado na fila do Service Bus com sucesso.", protocoloId);
                }

                // 3. Resposta assíncrona imediata (202 Accepted) sem tocar no banco de dados SQL Server
                return Accepted(new
                {
                    protocoloId = protocoloId,
                    mensagem = "Arquivo recebido com sucesso. Processamento em lote enfileirado.",
                    nomeArquivo = arquivo.FileName,
                    data = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha crítica durante a ingestão do arquivo {Arquivo}", arquivo.FileName);
                return StatusCode(StatusCodes.Status500InternalServerError, new { erro = "Falha ao enfileirar o arquivo para importação." });
            }
        }

        [HttpGet("{protocoloId:guid}/status")]
        public async Task<IActionResult> ObterStatus([FromRoute] Guid protocoloId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("ssp_ObterStatusConciliacaoProtocolo", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add(new SqlParameter("@ProtocoloImportacaoId", SqlDbType.UniqueIdentifier) { Value = protocoloId });

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                var total = Convert.ToInt32(reader["TotalRegistros"]);
                if (total == 0)
                    return NotFound(new { mensagem = "Protocolo não encontrado ou ainda não processado." });

                return Ok(new
                {
                    ProtocoloId = reader["ProtocoloId"],
                    TotalRegistros = total,
                    Conciliados = Convert.ToInt32(reader["Conciliados"]),
                    Rejeitados = Convert.ToInt32(reader["Rejeitados"]),
                    Pendentes = Convert.ToInt32(reader["Pendentes"]),
                    VolumeFinanceiroTotal = Convert.ToDecimal(reader["VolumeFinanceiroTotal"]),
                    VolumeConciliado = Convert.ToDecimal(reader["VolumeConciliado"])
                });
            }

            return NotFound(new { mensagem = "Protocolo não localizado." });
        }

        [HttpGet("{protocoloId:guid}/divergencias")]
        public async Task<IActionResult> ObterDivergencias([FromRoute] Guid protocoloId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("ssp_ObterDivergenciasProtocolo", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add(new SqlParameter("@ProtocoloImportacaoId", SqlDbType.UniqueIdentifier) { Value = protocoloId });

            await connection.OpenAsync();
            await using var reader = await command.ExecuteReaderAsync();

            var divergencias = new List<object>();
            while (await reader.ReadAsync())
            {
                divergencias.Add(new
                {
                    Id = reader["Id"],
                    DocumentoCliente = reader["DocumentoCliente"],
                    DataTransacao = reader["DataTransacao"],
                    Valor = reader["Valor"],
                    Tipo = reader["Tipo"],
                    MotivoRejeicao = reader["MotivoRejeicao"],
                    CriadoEm = reader["CriadoEm"]
                });
            }

            return Ok(new
            {
                ProtocoloId = protocoloId,
                TotalDivergencias = divergencias.Count,
                Itens = divergencias
            });
        }

        [HttpPost("{protocoloId:guid}/reprocessar")]
        public async Task<IActionResult> ReprocessarRejeitados([FromRoute] Guid protocoloId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await using var command = new SqlCommand("ssp_ReprocessarRejeitadosProtocolo", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.Add(new SqlParameter("@ProtocoloImportacaoId", SqlDbType.UniqueIdentifier) { Value = protocoloId });

            await connection.OpenAsync();
            await command.ExecuteNonQueryAsync();

            await using var cmdAudit = new SqlCommand("ssp_RegistrarLogAuditoria", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            cmdAudit.Parameters.Add(new SqlParameter("@ProtocoloId", SqlDbType.UniqueIdentifier) { Value = protocoloId });
            cmdAudit.Parameters.Add(new SqlParameter("@Nivel", SqlDbType.VarChar, 20) { Value = "INFO" });
            cmdAudit.Parameters.Add(new SqlParameter("@Origem", SqlDbType.VarChar, 100) { Value = "API" });
            cmdAudit.Parameters.Add(new SqlParameter("@Operacao", SqlDbType.VarChar, 100) { Value = "ReprocessarRejeitados" });
            cmdAudit.Parameters.Add(new SqlParameter("@Mensagem", SqlDbType.NVarChar, -1) { Value = "Reprocessamento de divergências solicitado com sucesso." });
            await cmdAudit.ExecuteNonQueryAsync();

            return Ok(new { mensagem = "Reprocessamento concluído com sucesso." });
        }

        [HttpPost("reprocessar-dlq")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> ReprocessarDlq([FromQuery] int limiteMensagens = 10, CancellationToken cancellationToken = default)
        {
            var queueName = _configuration["ServiceBusSettings:ImportacaoQueue"] ?? "importacao-lotes-queue";

            // Conecta na subfila Dead Letter Queue do Azure Service Bus
            var dlqReceiver = _serviceBusClient.CreateReceiver(queueName, new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter,
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

            var sender = _serviceBusClient.CreateSender(queueName);
            int totalResgatadas = 0;

            try
            {
                var mensagensDlq = await dlqReceiver.ReceiveMessagesAsync(
                    maxMessages: limiteMensagens,
                    maxWaitTime: TimeSpan.FromSeconds(3),
                    cancellationToken: cancellationToken);

                foreach (var msg in mensagensDlq)
                {
                    // Clona o payload preservando o corpo da mensagem
                    var mensagemReenviada = new ServiceBusMessage(msg.Body)
                    {
                        ContentType = msg.ContentType,
                        Subject = msg.Subject,
                        MessageId = $"{msg.MessageId}_retry_{DateTime.UtcNow.Ticks}"
                    };

                    // Copia os metadados e headers originais
                    foreach (var prop in msg.ApplicationProperties)
                    {
                        mensagemReenviada.ApplicationProperties.Add(prop.Key, prop.Value);
                    }

                    // Posta novamente na fila principal
                    await sender.SendMessageAsync(mensagemReenviada, cancellationToken);

                    // Confirma o dreno e remoção da DLQ
                    await dlqReceiver.CompleteMessageAsync(msg, cancellationToken);
                    totalResgatadas++;
                }

                return Ok(new
                {
                    mensagensResgatadas = totalResgatadas,
                    status = $"{totalResgatadas} lote(s) reenviado(s) da DLQ para a fila principal com sucesso."
                });
            }
            finally
            {
                await dlqReceiver.DisposeAsync();
                await sender.DisposeAsync();
            }
        }

        [HttpGet("inspecionar-dlq")]
        public async Task<IActionResult> InspecionarDlq([FromQuery] int quantidade = 5)
        {
            var queueName = _configuration["ServiceBusSettings:ImportacaoQueue"] ?? "importacao-lotes-queue";

            var receiver = _serviceBusClient.CreateReceiver(queueName, new ServiceBusReceiverOptions
            {
                SubQueue = SubQueue.DeadLetter
            });

            try
            {
                // PeekMessages apenas lê sem travar nem remover a mensagem da fila
                var mensagens = await receiver.PeekMessagesAsync(maxMessages: quantidade);

                var resultado = mensagens.Select(m => new
                {
                    m.MessageId,
                    DeadLetterReason = m.DeadLetterReason,
                    DeadLetterErrorDescription = m.DeadLetterErrorDescription,
                    Corpo = m.Body.ToString(),
                    DataEntradaDlq = m.EnqueuedTime
                });

                return Ok(resultado);
            }
            finally
            {
                await receiver.DisposeAsync();
            }
        }


        [HttpPost("testar-worker-direto")]
        public async Task<IActionResult> TestarWorkerDireto([FromBody] MensagemLoteImportacao mensagemTeste, CancellationToken cancellationToken)
        {
            var sender = _serviceBusClient.CreateSender("importacao-lotes-queue");

            var json = JsonSerializer.Serialize(mensagemTeste);
            var message = new ServiceBusMessage(json)
            {
                ContentType = "application/json",
                MessageId = Guid.NewGuid().ToString()
            };

            await sender.SendMessageAsync(message, cancellationToken);
            return Accepted(new { status = "Mensagem enviada diretamente para a fila com sucesso!" });
        }
    }
}