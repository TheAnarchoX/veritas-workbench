using Veritas.Domain;

namespace Veritas.Domain.Entities;

public sealed class DossierEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public Dossier? Dossier { get; set; }
    public DossierEntityKind Kind { get; set; } = DossierEntityKind.Other;
    public required string Name { get; set; }
    public string? Handle { get; set; }
    public string? Platform { get; set; }
    public string? Url { get; set; }
    public string? Notes { get; set; }
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.None;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<DossierEntityRelation> OutgoingRelations { get; set; } = [];
    public List<DossierEntityRelation> IncomingRelations { get; set; } = [];
    public List<Source> AuthoredSources { get; set; } = [];
}
