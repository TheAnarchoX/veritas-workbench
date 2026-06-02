using Veritas.Domain;

namespace Veritas.Domain.Entities;

public sealed class Claim
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public Dossier? Dossier { get; set; }
    public required string Text { get; set; }
    public ClaimStatus Status { get; set; } = ClaimStatus.Unassessed;
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.None;
    public string? Rationale { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ClaimEvidenceLink> EvidenceLinks { get; set; } = [];
}
