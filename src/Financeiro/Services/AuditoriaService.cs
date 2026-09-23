using Dapper;
using Financeiro.Domain.DTOs;
using Financeiro.Domain.Interfaces.Services;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Financeiro.Services
{
    public class AuditoriaService : IAuditoriaService
    {
        private readonly string _connectionString;

        public AuditoriaService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SqlDatabase")
                ?? throw new InvalidOperationException("Connection string 'SqlDatabase' ausente.");
        }

        public async Task RegistrarLogAsync(LogAuditoriaDTO log)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync("ssp_RegistrarLogAuditoria", log, commandType: CommandType.StoredProcedure);
        }
    }
}
