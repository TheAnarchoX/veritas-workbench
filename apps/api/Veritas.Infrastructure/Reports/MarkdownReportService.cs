using System.Text;
using Microsoft.EntityFrameworkCore;
using Veritas.Application.Reports;
using Veritas.Infrastructure.Persistence;

namespace Veritas.Infrastructure.Reports;

public sealed class MarkdownReportService(VeritasDbContext db) : IReportService
{
    public async Task<string> GenerateDossierMarkdownAsync(Guid dossierId, CancellationToken ct)
    {
        var dossier = await db.Dossiers
            .Include(x => x.Project)
            .Include(x => x.EvidenceItems)
            .Include(x => x.Sources)
            .Include(x => x.Claims)
            .Include(x => x.Findings)
            .Include(x => x.Tasks)
            .Include(x => x.TimelineEntries)
            .Include(x => x.Entities)
            .Include(x => x.EntityRelations)
            .FirstOrDefaultAsync(x => x.Id == dossierId, ct);

        if (dossier is null)
        {
            throw new InvalidOperationException("Dossier not found.");
        }

        var sb = new StringBuilder();
        sb.AppendLine($"# {dossier.Title}");
        sb.AppendLine();
        sb.AppendLine("## Executive summary");
        sb.AppendLine(Blank(dossier.Summary, "This report is generated from the current dossier state. Findings support or weaken claims but do not establish attribution without sufficient provenance."));
        sb.AppendLine();
        sb.AppendLine("## Scope and limitations");
        sb.AppendLine("This workspace supports media provenance, artifact review, source chronology, and conservative claim assessment. It is not an AI detector, does not establish attribution from a single heuristic, and does not identify private people.");
        sb.AppendLine();
        sb.AppendLine("## Evidence inventory");
        foreach (var item in dossier.EvidenceItems.OrderBy(x => x.CreatedAt))
        {
            sb.AppendLine($"- `{item.ContentHashSha256 ?? "unhashed"}` {item.Title} ({item.Type}, {item.ProvenanceStatus}, {item.MimeType ?? "unknown MIME"})");
        }
        sb.AppendLine();
        sb.AppendLine("## Claims assessed");
        foreach (var claim in dossier.Claims.OrderBy(x => x.CreatedAt))
        {
            sb.AppendLine($"- **{claim.Status} / {claim.Confidence}**: {claim.Text}");
            if (!string.IsNullOrWhiteSpace(claim.Rationale))
            {
                sb.AppendLine($"  - Rationale: {claim.Rationale}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("## Related entities");
        foreach (var entity in dossier.Entities.OrderBy(x => x.Kind).ThenBy(x => x.Name))
        {
            sb.AppendLine($"- **{entity.Kind} / {entity.Confidence}**: {entity.Name} {entity.Handle ?? ""} {entity.Platform ?? ""} {entity.Url ?? ""}".Trim());
            if (!string.IsNullOrWhiteSpace(entity.Notes))
            {
                sb.AppendLine($"  - Notes: {entity.Notes}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("## Entity relationships");
        var entityNames = dossier.Entities.ToDictionary(x => x.Id, x => x.Name);
        foreach (var relation in dossier.EntityRelations.OrderBy(x => x.RelationType).ThenBy(x => x.CreatedAt))
        {
            var from = entityNames.GetValueOrDefault(relation.FromEntityId, relation.FromEntityId.ToString());
            var to = entityNames.GetValueOrDefault(relation.ToEntityId, relation.ToEntityId.ToString());
            sb.AppendLine($"- **{relation.RelationType} / {relation.Confidence}**: {from} -> {to}");
            if (!string.IsNullOrWhiteSpace(relation.EvidenceBasis))
            {
                sb.AppendLine($"  - Evidence basis: {relation.EvidenceBasis}");
            }
            if (!string.IsNullOrWhiteSpace(relation.Notes))
            {
                sb.AppendLine($"  - Notes: {relation.Notes}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("## Timeline");
        foreach (var entry in dossier.TimelineEntries.OrderBy(x => x.Time))
        {
            sb.AppendLine($"- {entry.Time:u} | {entry.Platform ?? "unknown platform"} | {entry.Url ?? entry.Source ?? "manual"} | confidence: {entry.Confidence} | {entry.Caption ?? entry.Notes ?? ""}");
        }
        sb.AppendLine();
        sb.AppendLine("## Media forensic results");
        foreach (var finding in dossier.Findings.Where(x => x.Category is not Domain.FindingCategory.Provenance and not Domain.FindingCategory.RobotsPolicy).OrderByDescending(x => x.CreatedAt))
        {
            AppendFinding(sb, finding);
        }
        sb.AppendLine();
        sb.AppendLine("## Provenance analysis");
        foreach (var source in dossier.Sources.OrderBy(x => x.ObservedAt))
        {
            sb.AppendLine($"- {source.Platform ?? source.Type.ToString()} `{source.Url ?? source.Title ?? source.Id.ToString()}`: {source.CollectionStatus}. {source.Notes}");
        }
        foreach (var finding in dossier.Findings.Where(x => x.Category is Domain.FindingCategory.Provenance or Domain.FindingCategory.RobotsPolicy).OrderByDescending(x => x.CreatedAt))
        {
            AppendFinding(sb, finding);
        }
        sb.AppendLine();
        sb.AppendLine("## Open questions");
        foreach (var task in dossier.Tasks.Where(x => x.Status != Domain.InvestigationTaskStatus.Done).OrderByDescending(x => x.Priority))
        {
            sb.AppendLine($"- {task.Title}: {task.Description}");
        }
        sb.AppendLine();
        sb.AppendLine("## What would prove the allegation");
        sb.AppendLine("- Original source media with stable provenance, capture metadata where available, and corroborating earlier/later source chronology.");
        sb.AppendLine("- Independent source matches that predate suspected reposts or edits.");
        sb.AppendLine();
        sb.AppendLine("## What would disprove the allegation");
        sb.AppendLine("- A verified original file or platform original inconsistent with the alleged manipulation.");
        sb.AppendLine("- A credible chronology showing benign platform recompression, editing, or reposting explains the observed artifacts.");
        sb.AppendLine();
        sb.AppendLine("## Recommended next steps");
        foreach (var task in dossier.Tasks.OrderByDescending(x => x.Priority).ThenBy(x => x.CreatedAt).Take(10))
        {
            sb.AppendLine($"- [{task.Status}] {task.Title}");
        }
        sb.AppendLine();
        sb.AppendLine("## Appendix: hashes, metadata, analysis artifacts");
        foreach (var item in dossier.EvidenceItems.OrderBy(x => x.CreatedAt))
        {
            sb.AppendLine($"- {item.Title}: SHA-256 `{item.ContentHashSha256 ?? "not recorded"}`, size `{item.FileSizeBytes?.ToString() ?? "unknown"}` bytes, stored as immutable evidence item `{item.Id}`.");
        }

        return sb.ToString();
    }

    private static void AppendFinding(StringBuilder sb, Domain.Entities.Finding finding)
    {
        sb.AppendLine($"- **{finding.Category} / {finding.Direction} / {finding.Confidence}**: {finding.Claim}");
        sb.AppendLine($"  - Evidence: {finding.Evidence}");
        sb.AppendLine($"  - Limitations: {finding.Limitations}");
        sb.AppendLine($"  - Falsification path: {finding.FalsificationPath}");
    }

    private static string Blank(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
