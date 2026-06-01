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
    ILogger<AnalysisJobWorker> logger) : BackgroundService
{
    private readonly AnalysisOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VeritasDbContext>();
                var processor = scope.ServiceProvider.GetRequiredService<AnalysisRunProcessor>();
                var pendingRuns = await db.AnalysisRuns
                    .Where(x => x.Status == AnalysisStatus.Pending)
                    .ToListAsync(stoppingToken);
                var run = pendingRuns.OrderBy(x => x.StartedAt).FirstOrDefault();

                if (run is not null)
                {
                    await processor.ProcessAsync(run.Id, stoppingToken);
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
