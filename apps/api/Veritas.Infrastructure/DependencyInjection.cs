using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Veritas.Application.Analysis;
using Veritas.Application.Policies;
using Veritas.Application.Reports;
using Veritas.Application.Storage;
using Veritas.Infrastructure.Analysis;
using Veritas.Infrastructure.Persistence;
using Veritas.Infrastructure.Policies;
using Veritas.Infrastructure.Reports;
using Veritas.Infrastructure.Storage;

namespace Veritas.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddVeritasInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.Configure<RobotsOptions>(configuration.GetSection("Robots"));
        services.Configure<AnalysisOptions>(configuration.GetSection("Analysis"));
        services.Configure<ForensicsOptions>(configuration.GetSection("Forensics"));

        services.AddDbContext<VeritasDbContext>(options =>
        {
            var provider = configuration["Database:Provider"] ?? "Postgres";
            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlite(configuration.GetConnectionString("Sqlite") ?? "Data Source=veritas.db");
                return;
            }

            var connectionString = configuration.GetConnectionString("Postgres")
                ?? "Host=localhost;Port=5432;Database=veritas;Username=veritas;Password=veritas";
            options.UseNpgsql(connectionString);
        });

        services.AddSingleton<LocalEvidenceStorage>();
        services.AddSingleton<IEvidenceStorage>(sp => sp.GetRequiredService<LocalEvidenceStorage>());
        services.AddSingleton<IArtifactStorage>(sp => sp.GetRequiredService<LocalEvidenceStorage>());
        services.AddHttpClient<IRobotsPolicyService, RobotsPolicyService>();
        services.AddScoped<IAnalysisService, AnalysisService>();
        services.AddScoped<AnalysisRunProcessor>();
        services.AddScoped<IReportService, MarkdownReportService>();

        if (configuration.GetValue("Analysis:RunBackgroundWorker", true))
        {
            services.AddHostedService<AnalysisJobWorker>();
        }

        return services;
    }
}
