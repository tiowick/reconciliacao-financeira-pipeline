using Financeiro.Domain.Interfaces.Services;
using Financeiro.Domain.Models;
using System.Globalization;

namespace Financeiro.Services
{
    public class CsvStreamParserService : ICsvStreamParserService
    {
        public async IAsyncEnumerable<List<TransacaoFinanceira>> LerEmLotesAsync(
            Stream csvStream,
            Guid protocoloId,
            int tamanhoLote = 100,
            CancellationToken cancellationToken = default)
        {
            // StreamReader lê linha por linha diretamente do buffer de rede/disco
            using var reader = new StreamReader(csvStream);

            var loteAtual = new List<TransacaoFinanceira>(tamanhoLote);
            int numeroLinha = 0;

            while (!reader.EndOfStream)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var linha = await reader.ReadLineAsync(cancellationToken);
                numeroLinha++;

                // Ignora cabeçalho ou linhas em branco
                if (numeroLinha == 1 && linha != null && linha.Contains("DocumentoCliente", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(linha))
                    continue;

                var transacao = ConverterLinhaParaTransacao(linha, protocoloId);
                if (transacao != null)
                {
                    loteAtual.Add(transacao);
                }

                // Quando atinge o tamanho do lote, devolve sob demanda e limpa a lista
                if (loteAtual.Count >= tamanhoLote)
                {
                    yield return loteAtual;
                    loteAtual = new List<TransacaoFinanceira>(tamanhoLote);
                }
            }

            // Devolve o lote restante se houver registros
            if (loteAtual.Count > 0)
            {
                yield return loteAtual;
            }
        }

        private static TransacaoFinanceira? ConverterLinhaParaTransacao(string linha, Guid protocoloId)
        {
            // Detecta se o delimitador é ponto e vírgula ou vírgula
            char separador = linha.Contains(';') ? ';' : ',';
            var colunas = linha.Split(separador);

            if (colunas.Length < 4)
                return null;

            var documento = colunas[0].Trim();

            // Aceita formatos dd/MM/yyyy, yyyy-MM-dd e culturas comuns
            string[] formatosData = { "dd/MM/yyyy", "yyyy-MM-dd", "yyyy/MM/dd" };
            if (!DateTime.TryParseExact(colunas[1].Trim(), formatosData, CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
            {
                if (!DateTime.TryParse(colunas[1].Trim(), new CultureInfo("pt-BR"), DateTimeStyles.None, out data))
                    data = DateTime.UtcNow;
            }

            // Suporta tanto ponto quanto vírgula nos decimais
            var valorTexto = colunas[2].Trim().Replace(',', '.');
            if (!decimal.TryParse(valorTexto, NumberStyles.Any, CultureInfo.InvariantCulture, out var valor))
                valor = 0m;

            var tipo = colunas[3].Trim().ToUpperInvariant();

            return new TransacaoFinanceira
            {
                Id = Guid.NewGuid(),
                ProtocoloImportacaoId = protocoloId,
                DocumentoCliente = documento,
                DataTransacao = data,
                Valor = valor,
                Tipo = tipo,
                Status = "PENDENTE",
                CriadoEm = DateTime.UtcNow
            };
        }
    }
}
