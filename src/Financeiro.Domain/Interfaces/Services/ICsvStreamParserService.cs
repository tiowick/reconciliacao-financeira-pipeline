using Financeiro.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Financeiro.Domain.Interfaces.Services
{
    public interface ICsvStreamParserService
    {
        IAsyncEnumerable<List<TransacaoFinanceira>> LerEmLotesAsync(Stream csvStream, Guid protocoloId,
            int tamanhoLote = 100,
            CancellationToken cancellationToken = default);
    }
}
