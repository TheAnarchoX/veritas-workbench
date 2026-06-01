using Veritas.Domain;

namespace Veritas.Domain.Entities;

public sealed class AnalysisRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EvidenceItemId { get; set; }
    public EvidenceItem? EvidenceItem { get; set; }
    public required string Pipeline { get; set; }
    public AnalysisStatus Status { get; set; } = AnalysisStatus.Pending;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ToolVersion { get; set; }
    public string? ParametersJson { get; set; }
    public string? ResultJson { get; set; }
    public string? Summary { get; set; }
    public string? Error { get; set; }
    public List<Finding> Findings { get; set; } = [];
    public List<AnalysisArtifact> Artifacts { get; set; } = [];
}
