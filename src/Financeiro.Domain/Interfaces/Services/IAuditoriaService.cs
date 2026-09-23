using Financeiro.Domain.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Financeiro.Domain.Interfaces.Services
{
    public interface IAuditoriaService
    {
        Task RegistrarLogAsync(LogAuditoriaDTO log);
    }
}
