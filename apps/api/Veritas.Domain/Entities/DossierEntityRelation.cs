using Veritas.Domain;

namespace Veritas.Domain.Entities;

public sealed class DossierEntityRelation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public Dossier? Dossier { get; set; }
    public Guid FromEntityId { get; set; }
    public DossierEntity? FromEntity { get; set; }
    public Guid ToEntityId { get; set; }
    public DossierEntity? ToEntity { get; set; }
    public string RelationType { get; set; } = "related";
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.None;
    public string? EvidenceBasis { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
