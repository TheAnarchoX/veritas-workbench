using Veritas.Domain;

namespace Veritas.Domain.Entities;

public sealed class EvidenceItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public Dossier? Dossier { get; set; }
    public Guid? SourceId { get; set; }
    public Source? Source { get; set; }
    public EvidenceType Type { get; set; } = EvidenceType.Other;
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? OriginalFilename { get; set; }
    public string? ContentHashSha256 { get; set; }
    public string? PerceptualHash { get; set; }
    public required string StoragePath { get; set; }
    public string? MimeType { get; set; }
    public long? FileSizeBytes { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public double? DurationSeconds { get; set; }
    public DateTimeOffset? CapturedAt { get; set; }
    public DateTimeOffset UploadedAt { get; set; } = DateTimeOffset.UtcNow;
    public ProvenanceStatus ProvenanceStatus { get; set; } = ProvenanceStatus.Unknown;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<AnalysisRun> AnalysisRuns { get; set; } = [];
    public List<Finding> Findings { get; set; } = [];
    public List<ChainOfCustodyEvent> ChainOfCustodyEvents { get; set; } = [];
}
