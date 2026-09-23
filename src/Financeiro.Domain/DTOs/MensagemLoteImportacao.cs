using Financeiro.Domain.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Financeiro.Domain.DTOs
{
    [DebuggerStepThrough]
    public class MensagemLoteImportacao
    {
        public Guid ProtocoloId { get; set; } = default!;
        public int NumeroLote { get; set; } = default!;
        public int TotalLotes { get; set; } = default!;
        public string NomeArquivoOriginal { get; set; } = string.Empty;
        public string BlobUri { get; set; } = string.Empty;
        public List<TransacaoFinanceira> Transacoes { get; set; } = new();
        public DateTime DataPublicacao { get; set; } = DateTime.UtcNow;
    }
}
