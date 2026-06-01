using Veritas.Domain;

namespace Veritas.Domain.Entities;

public sealed class Source
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public Dossier? Dossier { get; set; }
    public SourceType Type { get; set; } = SourceType.Url;
    public string? Url { get; set; }
    public string? Platform { get; set; }
    public string? Title { get; set; }
    public string? AuthorHandle { get; set; }
    public DateTimeOffset? ObservedAt { get; set; }
    public DateTimeOffset? FirstSeenAt { get; set; }
    public CollectionStatus CollectionStatus { get; set; } = CollectionStatus.NotStarted;
    public string? RobotsDecision { get; set; }
    public string? Notes { get; set; }
    public List<EvidenceItem> EvidenceItems { get; set; } = [];
}
