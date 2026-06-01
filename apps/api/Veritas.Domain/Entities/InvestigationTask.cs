using Veritas.Domain;

namespace Veritas.Domain.Entities;

public sealed class InvestigationTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DossierId { get; set; }
    public Dossier? Dossier { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public InvestigationTaskStatus Status { get; set; } = InvestigationTaskStatus.Open;
    public Priority Priority { get; set; } = Priority.Medium;
    public InvestigationTaskType TaskType { get; set; } = InvestigationTaskType.Other;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
}
