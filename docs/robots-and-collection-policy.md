# Robots and Collection Policy

Collection is conservative by default.

The backend uses `IRobotsPolicyService` to check robots.txt before automated URL collection. A `RobotsDecision` records URI, host, allow/deny decision, reason, matched rule, checked time, robots.txt URL, robots.txt content hash, and crawl-delay where present.

Policy:

- Use a clear configurable user-agent.
- Cache robots decisions.
- Respect disallow rules and crawl-delay.
- Prefer official APIs and user-provided originals.
- Never bypass login walls, anti-bot systems, paywalls, or platform restrictions.
- If fetching is blocked, ambiguous, or terms-sensitive, create a manual collection task.

For X/Twitter URLs, the MVP records the source and asks the user to provide original media, a platform-original media URL, archive capture, screenshot, or metadata text. Screenshots are accepted but treated as weaker provenance than originals.
