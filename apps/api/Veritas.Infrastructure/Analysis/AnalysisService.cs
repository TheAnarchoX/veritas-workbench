using Microsoft.EntityFrameworkCore;
using Veritas.Application.Analysis;
using Veritas.Domain;
using Veritas.Domain.Entities;
using Veritas.Infrastructure.Persistence;

namespace Veritas.Infrastructure.Analysis;

public sealed class AnalysisService(VeritasDbContext db) : IAnalysisService
{
    public async Task<AnalysisRun> QueueAnalysisAsync(Guid evidenceItemId, string pipeline, CancellationToken ct)
    {
        var evidence = await db.EvidenceItems.FirstOrDefaultAsync(x => x.Id == evidenceItemId, ct);
        if (evidence is null)
        {
            throw new InvalidOperationException("Evidence item not found.");
        }

        var normalized = pipeline.Trim().ToLowerInvariant() switch
        {
            "image" or "imageanalysis" => "image",
            "video" or "videoanalysis" => "video",
            _ => throw new InvalidOperationException("Unsupported analysis pipeline.")
        };

        var run = new AnalysisRun
        {
            EvidenceItemId = evidenceItemId,
            Pipeline = normalized,
            Status = AnalysisStatus.Pending,
            ParametersJson = "{}",
            Summary = "Queued for forensic worker processing."
        };

        db.AnalysisRuns.Add(run);
        await db.SaveChangesAsync(ct);
        return run;
    }
}
