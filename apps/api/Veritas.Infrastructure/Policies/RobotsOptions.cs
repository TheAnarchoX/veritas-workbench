namespace Veritas.Infrastructure.Policies;

public sealed class RobotsOptions
{
    public string UserAgent { get; set; } = "VeritasWorkbench/0.1 (+local-first investigation workspace)";
    public int CacheHours { get; set; } = 6;
}
