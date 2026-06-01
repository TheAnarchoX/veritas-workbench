namespace Veritas.Application.Policies;

public sealed record RobotsDecision(
    string Uri,
    string Host,
    bool Allowed,
    string Reason,
    string? MatchedRule,
    DateTimeOffset CheckedAt,
    string RobotsTxtUrl,
    string? RobotsTxtContentHash,
    int? CrawlDelaySeconds = null);

public interface IRobotsPolicyService
{
    Task<RobotsDecision> CanFetchAsync(Uri uri, string userAgent, CancellationToken ct);
}
