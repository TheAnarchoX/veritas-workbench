using Veritas.Domain;

namespace Veritas.Domain.Entities;

public sealed class ClaimEvidenceLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }
    public Claim? Claim { get; set; }
    public Guid EvidenceItemId { get; set; }
    public EvidenceItem? EvidenceItem { get; set; }
    public EvidenceStance Stance { get; set; } = EvidenceStance.Contextual;
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
