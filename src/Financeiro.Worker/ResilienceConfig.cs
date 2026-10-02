using Microsoft.Data.SqlClient;
using Polly;
using Polly.Retry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Financeiro.Worker
{
    public static class ResilienceConfig
    {
        public static ResiliencePipeline ObterPipelineBanco()
        {
            return new ResiliencePipelineBuilder()
                .AddRetry(new RetryStrategyOptions
                {
                    ShouldHandle = new PredicateBuilder()
                    .Handle<SqlException>()                               
                    .Handle<TimeoutException>(),
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(1),
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true, // Evita que múltiplos workers tentem religar no mesmo milissegundo
                    OnRetry = args =>
                    {
                        Console.WriteLine($"[Polly] Falha transitória. Tentativa {args.AttemptNumber + 1}. Motivo: {args.Outcome.Exception?.Message}");
                        return ValueTask.CompletedTask;
                    }
                })
                .Build();
        }
    }
}
