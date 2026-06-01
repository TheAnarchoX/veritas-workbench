using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Veritas.Application.Policies;
using Veritas.Domain.Entities;
using Veritas.Infrastructure.Persistence;

namespace Veritas.Infrastructure.Policies;

public sealed class RobotsPolicyService(HttpClient httpClient, VeritasDbContext db, IOptions<RobotsOptions> options) : IRobotsPolicyService
{
    private readonly RobotsOptions _options = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<RobotsDecision> CanFetchAsync(Uri uri, string userAgent, CancellationToken ct)
    {
        var agent = string.IsNullOrWhiteSpace(userAgent) ? _options.UserAgent : userAgent;
        var cacheKeyUri = uri.GetLeftPart(UriPartial.Path);
        var now = DateTimeOffset.UtcNow;

        var cachedEntries = await db.RobotsCacheEntries
            .AsNoTracking()
            .Where(x => x.Host == uri.Host && x.UserAgent == agent && x.Uri == cacheKeyUri)
            .ToListAsync(ct);
        var cached = cachedEntries
            .Where(x => x.ExpiresAt > now)
            .OrderByDescending(x => x.CheckedAt)
            .FirstOrDefault();

        if (cached is not null)
        {
            var cachedDecision = JsonSerializer.Deserialize<RobotsDecision>(cached.DecisionJson, JsonOptions);
            if (cachedDecision is not null)
            {
                return cachedDecision;
            }
        }

        var robotsUri = new Uri($"{uri.Scheme}://{uri.Host}/robots.txt");
        RobotsDecision decision;

        try
        {
            using var response = await httpClient.GetAsync(robotsUri, ct);
            if (!response.IsSuccessStatusCode)
            {
                decision = new RobotsDecision(
                    uri.ToString(),
                    uri.Host,
                    false,
                    $"robots.txt returned HTTP {(int)response.StatusCode}; manual collection is required before fetching.",
                    null,
                    now,
                    robotsUri.ToString(),
                    null);
            }
            else
            {
                var robotsText = await response.Content.ReadAsStringAsync(ct);
                decision = EvaluateRobots(uri, robotsUri, robotsText, agent, now);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            decision = new RobotsDecision(
                uri.ToString(),
                uri.Host,
                false,
                $"robots.txt could not be checked ({ex.GetType().Name}); manual collection is required.",
                null,
                now,
                robotsUri.ToString(),
                null);
        }

        db.RobotsCacheEntries.Add(new RobotsCacheEntry
        {
            Host = uri.Host,
            UserAgent = agent,
            Uri = cacheKeyUri,
            DecisionJson = JsonSerializer.Serialize(decision, JsonOptions),
            CheckedAt = now,
            ExpiresAt = now.AddHours(Math.Max(1, _options.CacheHours))
        });
        await db.SaveChangesAsync(ct);

        return decision;
    }

    private static RobotsDecision EvaluateRobots(Uri uri, Uri robotsUri, string robotsText, string userAgent, DateTimeOffset checkedAt)
    {
        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(robotsText))).ToLowerInvariant();
        var groups = ParseGroups(robotsText);
        var matchingRules = groups
            .Where(group => group.UserAgents.Any(agent => agent == "*" || userAgent.Contains(agent, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(group => group.Rules.Select(rule => (Rule: rule, group.CrawlDelaySeconds)))
            .ToList();

        if (matchingRules.Count == 0)
        {
            return new RobotsDecision(uri.ToString(), uri.Host, true, "No matching robots.txt rule found.", null, checkedAt, robotsUri.ToString(), contentHash);
        }

        var path = string.IsNullOrWhiteSpace(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
        var matches = matchingRules
            .Where(x => path.StartsWith(x.Rule.Path, StringComparison.Ordinal))
            .OrderByDescending(x => x.Rule.Path.Length)
            .ToList();

        if (matches.Count == 0)
        {
            return new RobotsDecision(uri.ToString(), uri.Host, true, "No matching robots.txt path rule found.", null, checkedAt, robotsUri.ToString(), contentHash);
        }

        var selected = matches.First();
        var allowed = selected.Rule.Allow;
        var reason = allowed
            ? "robots.txt allows this path for the configured user-agent."
            : "robots.txt disallows this path; manual collection is required.";

        return new RobotsDecision(
            uri.ToString(),
            uri.Host,
            allowed,
            reason,
            $"{(allowed ? "Allow" : "Disallow")}: {selected.Rule.Path}",
            checkedAt,
            robotsUri.ToString(),
            contentHash,
            selected.CrawlDelaySeconds);
    }

    private static List<RobotsGroup> ParseGroups(string robotsText)
    {
        var groups = new List<RobotsGroup>();
        var current = new RobotsGroup();

        foreach (var rawLine in robotsText.Split('\n'))
        {
            var line = rawLine.Split('#')[0].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                if (current.UserAgents.Count > 0 || current.Rules.Count > 0)
                {
                    groups.Add(current);
                    current = new RobotsGroup();
                }

                continue;
            }

            var parts = line.Split(':', 2);
            if (parts.Length != 2)
            {
                continue;
            }

            var key = parts[0].Trim();
            var value = parts[1].Trim();

            if (key.Equals("User-agent", StringComparison.OrdinalIgnoreCase))
            {
                if (current.Rules.Count > 0)
                {
                    groups.Add(current);
                    current = new RobotsGroup();
                }

                current.UserAgents.Add(value);
            }
            else if (key.Equals("Allow", StringComparison.OrdinalIgnoreCase) && current.UserAgents.Count > 0)
            {
                current.Rules.Add(new RobotsRule(true, string.IsNullOrWhiteSpace(value) ? "/" : value));
            }
            else if (key.Equals("Disallow", StringComparison.OrdinalIgnoreCase) && current.UserAgents.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    current.Rules.Add(new RobotsRule(false, value));
                }
            }
            else if (key.Equals("Crawl-delay", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var delay))
            {
                current.CrawlDelaySeconds = delay;
            }
        }

        if (current.UserAgents.Count > 0 || current.Rules.Count > 0)
        {
            groups.Add(current);
        }

        return groups;
    }

    private sealed class RobotsGroup
    {
        public List<string> UserAgents { get; } = [];
        public List<RobotsRule> Rules { get; } = [];
        public int? CrawlDelaySeconds { get; set; }
    }

    private sealed record RobotsRule(bool Allow, string Path);
}
