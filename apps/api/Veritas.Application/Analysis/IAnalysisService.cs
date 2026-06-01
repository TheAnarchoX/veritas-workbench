using Veritas.Domain.Entities;

namespace Veritas.Application.Analysis;

public interface IAnalysisService
{
    Task<AnalysisRun> QueueAnalysisAsync(Guid evidenceItemId, string pipeline, CancellationToken ct);
}
