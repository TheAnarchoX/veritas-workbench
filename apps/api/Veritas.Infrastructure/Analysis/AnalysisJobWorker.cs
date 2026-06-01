using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Veritas.Domain;
using Veritas.Infrastructure.Persistence;

namespace Veritas.Infrastructure.Analysis;

public sealed class AnalysisJobWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<AnalysisOptions> options,
    IOptions<ForensicsOptions> forensicsOptions,
    ILogger<AnalysisJobWorker> logger) : BackgroundService
{
    private readonly AnalysisOptions _options = options.Value;
    private readonly ForensicsOptions _forensicsOptions = forensicsOptions.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VeritasDbContext>();
                var processor = scope.ServiceProvider.GetRequiredService<AnalysisRunProcessor>();
                var staleRunningCutoff = DateTimeOffset.UtcNow.AddSeconds(-Math.Max(5, _forensicsOptions.TimeoutSeconds));
                var candidateRuns = await db.AnalysisRuns
                    .Where(x => x.Status == AnalysisStatus.Pending || x.Status == AnalysisStatus.Running)
                    .ToListAsync(stoppingToken);
                var runs = candidateRuns
                    .Where(x => x.Status == AnalysisStatus.Pending || (x.StartedAt ?? DateTimeOffset.MinValue) <= staleRunningCutoff)
                    .OrderBy(x => x.Status == AnalysisStatus.Running ? 0 : 1)
                    .ThenBy(x => x.StartedAt ?? DateTimeOffset.MinValue)
                    .Take(25)
                    .ToList();

                foreach (var run in runs)
                {
                    await processor.ProcessAsync(run.Id, stoppingToken);
                }

                if (runs.Count > 0)
                {
                    continue;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Analysis job worker failed while polling.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(1, _options.PollSeconds)), stoppingToken);
        }
    }
}
