namespace Veritas.Domain.Entities;

public sealed class RobotsCacheEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Host { get; set; }
    public required string UserAgent { get; set; }
    public required string Uri { get; set; }
    public required string DecisionJson { get; set; }
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddHours(6);
}
