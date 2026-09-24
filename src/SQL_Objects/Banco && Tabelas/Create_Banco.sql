IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'ReconciliacaoDb')
BEGIN
    CREATE DATABASE ReconciliacaoDb;
END;
GO

USE ReconciliacaoDb;
GO

-- 1. Tabela Principal de Transações
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TransacoesFinanceiras')
BEGIN
    CREATE TABLE dbo.TransacoesFinanceiras (
        Id BIGINT IDENTITY(1,1) PRIMARY KEY,
        ProtocoloImportacaoId UNIQUEIDENTIFIER NOT NULL,
        DocumentoCliente VARCHAR(20) NOT NULL,
        DataTransacao DATETIME2(3) NOT NULL,
        Valor DECIMAL(18, 2) NOT NULL,
        Tipo VARCHAR(10) NOT NULL,
        Status VARCHAR(20) NOT NULL DEFAULT 'PENDENTE',
        MotivoRejeicao VARCHAR(255) NULL,
        CriadoEm DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
    );

    CREATE NONCLUSTERED INDEX IX_TransacoesFinanceiras_Protocolo 
        ON dbo.TransacoesFinanceiras (ProtocoloImportacaoId);
END;
GO

-- 2. Eliminação preventiva da procedure que consome o TVP para permitir recriação do tipo
IF OBJECT_ID('dbo.ssp_InserirLoteTransacoesFinanceiras', 'P') IS NOT NULL
    DROP PROCEDURE dbo.ssp_InserirLoteTransacoesFinanceiras;
GO

-- 3. TVP Estruturado com as 9 colunas requeridas pelo SqlBatchHelper.ToTable
IF EXISTS (SELECT * FROM sys.types WHERE is_table_type = 1 AND name = 'TransacaoFinanceiraType')
    DROP TYPE dbo.TransacaoFinanceiraType;
GO

CREATE TYPE dbo.TransacaoFinanceiraType AS TABLE (
    Id UNIQUEIDENTIFIER NULL,
    ProtocoloImportacaoId UNIQUEIDENTIFIER NOT NULL,
    DocumentoCliente VARCHAR(20) NOT NULL,
    DataTransacao DATETIME2(3) NOT NULL,
    Valor DECIMAL(18, 2) NOT NULL,
    Tipo VARCHAR(10) NOT NULL,
    Status VARCHAR(20) NOT NULL,
    MotivoRejeicao VARCHAR(255) NULL,
    CriadoEm DATETIME2(3) NULL
);
GO

-- 4. Tabela de Logs de Auditoria com suporte aos campos do LogAuditoriaDTO
IF OBJECT_ID('dbo.LogsAuditoria', 'U') IS NOT NULL
    DROP TABLE dbo.LogsAuditoria;
GO

CREATE TABLE dbo.LogsAuditoria (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    DataHora DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    ProtocoloId UNIQUEIDENTIFIER NULL,
    Nivel VARCHAR(20) NOT NULL,
    Origem VARCHAR(100) NOT NULL,
    Operacao VARCHAR(100) NULL,
    Mensagem NVARCHAR(MAX) NOT NULL,
    DetalhesJson NVARCHAR(MAX) NULL
);
GO

CREATE NONCLUSTERED INDEX IX_LogsAuditoria_DataHora ON dbo.LogsAuditoria (DataHora);
CREATE NONCLUSTERED INDEX IX_LogsAuditoria_ProtocoloId ON dbo.LogsAuditoria (ProtocoloId);
GO

-- 5. Procedure de Registo de Auditoria
CREATE OR ALTER PROCEDURE dbo.ssp_RegistrarLogAuditoria
    @ProtocoloId UNIQUEIDENTIFIER = NULL,
    @Nivel VARCHAR(20) = 'INFO',
    @Origem VARCHAR(100) = 'API',
    @Operacao VARCHAR(100) = NULL,
    @Mensagem NVARCHAR(MAX) = NULL,
    @DetalhesJson NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.LogsAuditoria (
        ProtocoloId,
        Nivel,
        Origem,
        Operacao,
        Mensagem,
        DetalhesJson
    )
    VALUES (
        @ProtocoloId,
        ISNULL(@Nivel, 'INFO'),
        ISNULL(@Origem, 'API'),
        @Operacao,
        @Mensagem,
        @DetalhesJson
    );
END;
GO

-- 6. Procedure de Inserção em Massa via TVP (Bulk Insert)
CREATE OR ALTER PROCEDURE dbo.ssp_InserirLoteTransacoesFinanceiras
    @Transacoes dbo.TransacaoFinanceiraType READONLY
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO dbo.TransacoesFinanceiras (
        ProtocoloImportacaoId,
        DocumentoCliente,
        DataTransacao,
        Valor,
        Tipo,
        Status,
        MotivoRejeicao,
        CriadoEm
    )
    SELECT 
        ProtocoloImportacaoId,
        DocumentoCliente,
        DataTransacao,
        Valor,
        Tipo,
        ISNULL(Status, 'PENDENTE'),
        MotivoRejeicao,
        ISNULL(CriadoEm, SYSUTCDATETIME())
    FROM @Transacoes;
END;
GO

-- 7. Motor de Conciliação e Validação de Negócio
CREATE OR ALTER PROCEDURE dbo.ssp_ConciliarTransacoesProtocolo
    @ProtocoloImportacaoId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- Rejeitar valores menores ou iguais a zero
    UPDATE dbo.TransacoesFinanceiras
    SET Status = 'REJEITADO',
        MotivoRejeicao = 'Valor da transacao invalido (<= 0)'
    WHERE ProtocoloImportacaoId = @ProtocoloImportacaoId
      AND Status = 'PENDENTE'
      AND Valor <= 0;

    -- Rejeitar documentos fora do padrão (CPF=11, CNPJ=14)
    UPDATE dbo.TransacoesFinanceiras
    SET Status = 'REJEITADO',
        MotivoRejeicao = 'Documento invalido (deve possuir 11 ou 14 digitos)'
    WHERE ProtocoloImportacaoId = @ProtocoloImportacaoId
      AND Status = 'PENDENTE'
      AND LEN(DocumentoCliente) NOT IN (11, 14);

    -- Rejeitar registos duplicados no mesmo lote de protocolo
    ;WITH Duplicadas AS (
        SELECT Id,
               ROW_NUMBER() OVER (
                   PARTITION BY DocumentoCliente, DataTransacao, Valor, Tipo 
                   ORDER BY Id
               ) AS Ocorrencia
        FROM dbo.TransacoesFinanceiras
        WHERE ProtocoloImportacaoId = @ProtocoloImportacaoId
          AND Status = 'PENDENTE'
    )
    UPDATE t
    SET Status = 'REJEITADO',
        MotivoRejeicao = 'Transacao duplicada no lote'
    FROM dbo.TransacoesFinanceiras t
    INNER JOIN Duplicadas d ON t.Id = d.Id
    WHERE d.Ocorrencia > 1;

    -- Promover as restantes transações válidas a CONCILIADO
    UPDATE dbo.TransacoesFinanceiras
    SET Status = 'CONCILIADO'
    WHERE ProtocoloImportacaoId = @ProtocoloImportacaoId
      AND Status = 'PENDENTE';
END;
GO

-- 8. Procedure de Consulta de Estado Consolidado
CREATE OR ALTER PROCEDURE dbo.ssp_ObterStatusConciliacaoProtocolo
    @ProtocoloImportacaoId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        @ProtocoloImportacaoId AS ProtocoloId,
        COUNT(1) AS TotalRegistros,
        ISNULL(SUM(CASE WHEN Status = 'CONCILIADO' THEN 1 ELSE 0 END), 0) AS Conciliados,
        ISNULL(SUM(CASE WHEN Status = 'REJEITADO' THEN 1 ELSE 0 END), 0) AS Rejeitados,
        ISNULL(SUM(CASE WHEN Status = 'PENDENTE' THEN 1 ELSE 0 END), 0) AS Pendentes,
        ISNULL(SUM(Valor), 0.00) AS VolumeFinanceiroTotal,
        ISNULL(SUM(CASE WHEN Status = 'CONCILIADO' THEN Valor ELSE 0 END), 0.00) AS VolumeConciliado
    FROM dbo.TransacoesFinanceiras
    WHERE ProtocoloImportacaoId = @ProtocoloImportacaoId;
END;
GO

-- 9. Procedure de Obtenção de Divergências Detalhadas
CREATE OR ALTER PROCEDURE dbo.ssp_ObterDivergenciasProtocolo
    @ProtocoloImportacaoId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT 
        Id,
        DocumentoCliente,
        DataTransacao,
        Valor,
        Tipo,
        MotivoRejeicao,
        CriadoEm
    FROM dbo.TransacoesFinanceiras
    WHERE ProtocoloImportacaoId = @ProtocoloImportacaoId
      AND Status = 'REJEITADO';
END;
GO

-- 10. Procedure de Reprocessamento de Divergências
CREATE OR ALTER PROCEDURE dbo.ssp_ReprocessarRejeitadosProtocolo
    @ProtocoloImportacaoId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- Repor transações rejeitadas em estado PENDENTE para nova análise
    UPDATE dbo.TransacoesFinanceiras
    SET Status = 'PENDENTE',
        MotivoRejeicao = NULL
    WHERE ProtocoloImportacaoId = @ProtocoloImportacaoId
      AND Status = 'REJEITADO';

    -- Reexecutar o motor de conciliação
    EXEC dbo.ssp_ConciliarTransacoesProtocolo @ProtocoloImportacaoId = @ProtocoloImportacaoId;
END;
GO