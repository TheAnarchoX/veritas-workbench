namespace Veritas.Domain.Entities;

public sealed class AnalysisArtifact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AnalysisRunId { get; set; }
    public AnalysisRun? AnalysisRun { get; set; }
    public Guid EvidenceItemId { get; set; }
    public EvidenceItem? EvidenceItem { get; set; }
    public required string Filename { get; set; }
    public required string StorageKey { get; set; }
    public string? ContentType { get; set; }
    public string? ArtifactType { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
