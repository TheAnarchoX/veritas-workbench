using Veritas.Domain;

namespace Veritas.Domain.Entities;

public sealed class TimelineEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public Dossier? Dossier { get; set; }
    public DateTimeOffset Time { get; set; } = DateTimeOffset.UtcNow;
    public string? Platform { get; set; }
    public string? Url { get; set; }
    public string? Source { get; set; }
    public string? EvidenceHash { get; set; }
    public string? Caption { get; set; }
    public bool FirstKnownAppearance { get; set; }
    public string? Notes { get; set; }
    public ConfidenceLevel Confidence { get; set; } = ConfidenceLevel.None;
}
