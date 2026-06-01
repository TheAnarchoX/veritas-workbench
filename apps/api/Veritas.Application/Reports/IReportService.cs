namespace Veritas.Application.Reports;

public interface IReportService
{
    Task<string> GenerateDossierMarkdownAsync(Guid dossierId, CancellationToken ct);
}
