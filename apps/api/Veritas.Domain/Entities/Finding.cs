using Veritas.Domain;

namespace Veritas.Domain.Entities;

public sealed class Finding
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public Dossier? Dossier { get; set; }
    public Guid? EvidenceItemId { get; set; }
    public EvidenceItem? EvidenceItem { get; set; }
    public Guid? AnalysisRunId { get; set; }
    public AnalysisRun? AnalysisRun { get; set; }
    public FindingCategory Category { get; set; } = FindingCategory.Other;
    public required string Claim { get; set; }
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.None;
    public FindingDirection Direction { get; set; } = FindingDirection.Inconclusive;
    public required string Evidence { get; set; }
    public required string Limitations { get; set; }
    public required string FalsificationPath { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
