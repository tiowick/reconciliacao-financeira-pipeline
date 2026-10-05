using System.Diagnostics.Metrics;

namespace Financeiro.Worker;

public static class WorkerMetrics
{
    public const string MeterName = "Financeiro.Worker";
    private static readonly Meter Meter = new(MeterName, "1.0.0");

    // Contador de lotes finalizados com sucesso
    public static readonly Counter<long> LotesProcessadosSucesso =
        Meter.CreateCounter<long>(
            name: "reconciliacao_lotes_sucesso",
            description: "Total de lotes conciliados e salvos com sucesso.");

    // Contador de retentativas executadas pelo Polly
    public static readonly Counter<long> RetentativasPolly =
        Meter.CreateCounter<long>(
            name: "reconciliacao_retentativas",
            description: "Total de retries disparados pelo pipeline Polly.");

    // Contador de mensagens encaminhadas para a DLQ
    public static readonly Counter<long> MensagensDlq =
        Meter.CreateCounter<long>(
            name: "reconciliacao_mensagens_dlq",
            description: "Total de lotes descartados na Dead Letter Queue.");
}