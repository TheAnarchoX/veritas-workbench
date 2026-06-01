using Veritas.Domain;
using Veritas.Domain.Entities;

namespace Veritas.Api.Endpoints;

public sealed record ProjectDto(Guid Id, string Name, string? Description, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int DossierCount)
{
    public static ProjectDto From(Project project) => new(project.Id, project.Name, project.Description, project.CreatedAt, project.UpdatedAt, project.Dossiers.Count);
}

public sealed record DossierDto(Guid Id, Guid ProjectId, string Title, string? Summary, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static DossierDto From(Dossier dossier) => new(dossier.Id, dossier.ProjectId, dossier.Title, dossier.Summary, dossier.Status.ToString(), dossier.CreatedAt, dossier.UpdatedAt);
}

public sealed record SourceDto(Guid Id, Guid DossierId, string Type, string? Url, string? Platform, string? Title, string? AuthorHandle, DateTimeOffset? ObservedAt, DateTimeOffset? FirstSeenAt, string CollectionStatus, string? RobotsDecision, string? Notes)
{
    public static SourceDto From(Source source) => new(source.Id, source.DossierId, source.Type.ToString(), source.Url, source.Platform, source.Title, source.AuthorHandle, source.ObservedAt, source.FirstSeenAt, source.CollectionStatus.ToString(), source.RobotsDecision, source.Notes);
}

public sealed record EvidenceDto(Guid Id, Guid DossierId, Guid? SourceId, string Type, string Title, string? Description, string? OriginalFilename, string? ContentHashSha256, string? PerceptualHash, string? MimeType, long? FileSizeBytes, int? Width, int? Height, double? DurationSeconds, DateTimeOffset? CapturedAt, DateTimeOffset UploadedAt, string ProvenanceStatus, string FileUrl, IReadOnlyList<AnalysisRunDto> AnalysisRuns)
{
    public static EvidenceDto From(EvidenceItem item) => new(
        item.Id,
        item.DossierId,
        item.SourceId,
        item.Type.ToString(),
        item.Title,
        item.Description,
        item.OriginalFilename,
        item.ContentHashSha256,
        item.PerceptualHash,
        item.MimeType,
        item.FileSizeBytes,
        item.Width,
        item.Height,
        item.DurationSeconds,
        item.CapturedAt,
        item.UploadedAt,
        item.ProvenanceStatus.ToString(),
        $"/api/evidence/{item.Id}/file",
        item.AnalysisRuns.OrderByDescending(x => x.StartedAt ?? DateTimeOffset.MinValue).Select(AnalysisRunDto.From).ToArray());
}

public sealed record AnalysisRunDto(Guid Id, Guid EvidenceItemId, string Pipeline, string Status, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? ToolVersion, string? Summary, string? Error, IReadOnlyList<AnalysisArtifactDto> Artifacts)
{
    public static AnalysisRunDto From(AnalysisRun run) => new(run.Id, run.EvidenceItemId, run.Pipeline, run.Status.ToString(), run.StartedAt, run.CompletedAt, run.ToolVersion, run.Summary, run.Error, run.Artifacts.Select(AnalysisArtifactDto.From).ToArray());
}

public sealed record AnalysisArtifactDto(Guid Id, Guid AnalysisRunId, string Filename, string? ContentType, string? ArtifactType, string DownloadUrl)
{
    public static AnalysisArtifactDto From(AnalysisArtifact artifact) => new(artifact.Id, artifact.AnalysisRunId, artifact.Filename, artifact.ContentType, artifact.ArtifactType, $"/api/analysis-artifacts/{artifact.Id}");
}

public sealed record FindingDto(Guid Id, Guid DossierId, Guid? EvidenceItemId, Guid? AnalysisRunId, string Category, string Claim, string Confidence, string Direction, string Evidence, string Limitations, string FalsificationPath, DateTimeOffset CreatedAt)
{
    public static FindingDto From(Finding finding) => new(finding.Id, finding.DossierId, finding.EvidenceItemId, finding.AnalysisRunId, finding.Category.ToString(), finding.Claim, finding.Confidence.ToString(), finding.Direction.ToString(), finding.Evidence, finding.Limitations, finding.FalsificationPath, finding.CreatedAt);
}

public sealed record ClaimDto(Guid Id, Guid DossierId, string Text, string Status, string Confidence, string? Rationale, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static ClaimDto From(Claim claim) => new(claim.Id, claim.DossierId, claim.Text, claim.Status.ToString(), claim.Confidence.ToString(), claim.Rationale, claim.CreatedAt, claim.UpdatedAt);
}

public sealed record InvestigationTaskDto(Guid Id, Guid DossierId, string Title, string? Description, string Status, string Priority, string TaskType, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt)
{
    public static InvestigationTaskDto From(InvestigationTask task) => new(task.Id, task.DossierId, task.Title, task.Description, task.Status.ToString(), task.Priority.ToString(), task.TaskType.ToString(), task.CreatedAt, task.CompletedAt);
}

public sealed record TimelineEntryDto(Guid Id, Guid DossierId, DateTimeOffset Time, string? Platform, string? Url, string? Source, string? EvidenceHash, string? Caption, bool FirstKnownAppearance, string? Notes, string Confidence)
{
    public static TimelineEntryDto From(TimelineEntry entry) => new(entry.Id, entry.DossierId, entry.Time, entry.Platform, entry.Url, entry.Source, entry.EvidenceHash, entry.Caption, entry.FirstKnownAppearance, entry.Notes, entry.Confidence.ToString());
}

public sealed record CreateProjectRequest(string Name, string? Description);
public sealed record CreateDossierRequest(string Title, string? Summary);
public sealed record AddUrlSourceRequest(string Url, string? Title, string? AuthorHandle, DateTimeOffset? ObservedAt);
public sealed record CreateFindingRequest(Guid? EvidenceItemId, Guid? AnalysisRunId, string Category, string Claim, string Confidence, string Direction, string Evidence, string Limitations, string FalsificationPath);
public sealed record CreateClaimRequest(string Text, string? Status, string? Confidence, string? Rationale);
public sealed record PatchClaimRequest(string? Status, string? Confidence, string? Rationale);
public sealed record CreateTaskRequest(string Title, string? Description, string? Priority, string? TaskType);
public sealed record PatchTaskRequest(string? Status, string? Priority, string? Description);
public sealed record CreateTimelineEntryRequest(DateTimeOffset? Time, string? Platform, string? Url, string? Source, string? EvidenceHash, string? Caption, bool? FirstKnownAppearance, string? Notes, string? Confidence);
