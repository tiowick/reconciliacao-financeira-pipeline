using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Financeiro.Domain.DTOs
{
    [DebuggerStepThrough]
    public class LogAuditoriaDTO
    {
        public Guid? ProtocoloId { get; init; } = default!;
        public string? Origem { get; init; } = default!;      // "API", "Worker"
        public string Nivel { get; init; } = "INFO";              // "INFO", "WARN", "ERROR"
        public string? Operacao { get; init; } = default!;     // "UploadArquivo", "Conciliacao"
        public string? Mensagem { get; init; } = default!;
        public string? DetalhesJson { get; init; } = default!;
    }
}
