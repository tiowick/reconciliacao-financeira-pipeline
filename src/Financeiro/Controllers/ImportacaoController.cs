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
        private readonly ICsvStreamParserService _csvParser;
        private readonly IConfiguration _configuration;
        private readonly ILogger<ImportacaoController> _logger;
        private readonly IAuditoriaService _auditoriaService;
        private readonly string _connectionString;

        public ImportacaoController(
            BlobServiceClient blobServiceClient,
            ServiceBusClient serviceBusClient,
            ICsvStreamParserService csvParser,
            IConfiguration configuration,
            ILogger<ImportacaoController> logger,
            IAuditoriaService auditoriaService)
        {
            _blobServiceClient = blobServiceClient;
            _serviceBusClient = serviceBusClient;
            _csvParser = csvParser;
            _configuration = configuration;
            _logger = logger;
            _auditoriaService = auditoriaService;

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
                // 1. Salvar o arquivo bruto no Blob Storage (Azurite)
                var containerName = _configuration["BlobStorageSettings:ContainerName"] ?? "arquivos-importacao";
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                var blobClient = containerClient.GetBlobClient(nomeBlob);
                await using (var uploadStream = arquivo.OpenReadStream())
                {
                    await blobClient.UploadAsync(uploadStream, overwrite: true, cancellationToken);
                }

                _logger.LogInformation("Arquivo {Arquivo} salvo no Blob Storage com sucesso. Protocolo: {ProtocoloId}", arquivo.FileName, protocoloId);

                // 2. Preparar sender do Azure Service Bus
                var queueName = _configuration["ServiceBusSettings:ImportacaoQueue"] ?? "importacao-lotes-queue";
                var sender = _serviceBusClient.CreateSender(queueName);

                // 3. Ler o arquivo em lotes via streaming e despachar as mensagens
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

                        await sender.SendMessageAsync(serviceBusMessage, cancellationToken);
                    }

                    _logger.LogInformation("Protocolo {ProtocoloId} fatiado e postado na fila {Queue} com sucesso.", protocoloId, queueName);
                }

                // 4. Registrar auditoria estruturada via DTO
                await _auditoriaService.RegistrarLogAsync(new LogAuditoriaDTO
                {
                    ProtocoloId = protocoloId,
                    Origem = "API",
                    Nivel = "INFO",
                    Operacao = "UploadArquivo",
                    Mensagem = $"Arquivo {arquivo.FileName} ingerido e enfileirado com sucesso.",
                    DetalhesJson = JsonSerializer.Serialize(new { TamanhoBytes = arquivo.Length, NomeBlob = nomeBlob })
                });

                // 5. Retornar resposta assíncrona imediata (202 Accepted)
                return Accepted(new
                {
                    ProtocoloId = protocoloId,
                    Mensagem = "Arquivo recebido com sucesso. Processamento em lote enfileirado.",
                    NomeArquivo = arquivo.FileName,
                    Data = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                // Registra a falha na tabela de auditoria via DTO
                await _auditoriaService.RegistrarLogAsync(new LogAuditoriaDTO
                {
                    ProtocoloId = protocoloId,
                    Origem = "API",
                    Nivel = "ERROR",
                    Operacao = "UploadArquivo",
                    Mensagem = $"Erro ao processar o upload: {ex.Message}",
                    DetalhesJson = JsonSerializer.Serialize(new { ex.StackTrace })
                });

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

            await _auditoriaService.RegistrarLogAsync(new LogAuditoriaDTO
            {
                ProtocoloId = protocoloId,
                Origem = "API",
                Nivel = "INFO",
                Operacao = "ReprocessarRejeitados",
                Mensagem = "Reprocessamento de divergências solicitado com sucesso."
            });

            return Ok(new { mensagem = "Reprocessamento concluído com sucesso." });
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