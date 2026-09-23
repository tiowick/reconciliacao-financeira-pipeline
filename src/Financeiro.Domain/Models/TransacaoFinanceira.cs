using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Financeiro.Domain.Models
{
    [DebuggerStepThrough]
    public class TransacaoFinanceira
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProtocoloImportacaoId { get; set; } = default!;
        public string DocumentoCliente { get; set; } = string.Empty;
        public DateTime DataTransacao { get; set; } = default!;
        public decimal Valor { get; set; } = default!;
        public string Tipo { get; set; } = string.Empty; // "CREDITO" ou "DEBITO"
        public string Status { get; set; } = "PENDENTE"; // "PROCESSADO", "REJEITADO"
        public string? MotivoRejeicao { get; set; } = default!;
        public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    }
}
