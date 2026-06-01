using Veritas.Domain;

namespace Veritas.Domain.Entities;

public sealed class Dossier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }
    public required string Title { get; set; }
    public string? Summary { get; set; }
    public DossierStatus Status { get; set; } = DossierStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Source> Sources { get; set; } = [];
    public List<EvidenceItem> EvidenceItems { get; set; } = [];
    public List<Finding> Findings { get; set; } = [];
    public List<Claim> Claims { get; set; } = [];
    public List<InvestigationTask> Tasks { get; set; } = [];
    public List<TimelineEntry> TimelineEntries { get; set; } = [];
}
