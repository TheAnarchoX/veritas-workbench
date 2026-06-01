using Veritas.Domain;

namespace Veritas.Domain.Entities;

public sealed class ChainOfCustodyEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EvidenceItemId { get; set; }
    public EvidenceItem? EvidenceItem { get; set; }
    public CustodyEventType EventType { get; set; }
    public string? Actor { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? DetailsJson { get; set; }
}
