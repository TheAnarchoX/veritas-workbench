using Microsoft.EntityFrameworkCore;
using Veritas.Domain;
using Veritas.Domain.Entities;

namespace Veritas.Infrastructure.Persistence;

public sealed class VeritasDbContext(DbContextOptions<VeritasDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Dossier> Dossiers => Set<Dossier>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<EvidenceItem> EvidenceItems => Set<EvidenceItem>();
    public DbSet<AnalysisRun> AnalysisRuns => Set<AnalysisRun>();
    public DbSet<AnalysisArtifact> AnalysisArtifacts => Set<AnalysisArtifact>();
    public DbSet<Finding> Findings => Set<Finding>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<InvestigationTask> InvestigationTasks => Set<InvestigationTask>();
    public DbSet<ChainOfCustodyEvent> ChainOfCustodyEvents => Set<ChainOfCustodyEvent>();
    public DbSet<TimelineEntry> TimelineEntries => Set<TimelineEntry>();
    public DbSet<DossierEntity> DossierEntities => Set<DossierEntity>();
    public DbSet<DossierEntityRelation> DossierEntityRelations => Set<DossierEntityRelation>();
    public DbSet<RobotsCacheEntry> RobotsCacheEntries => Set<RobotsCacheEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(180);
            entity.HasMany(x => x.Dossiers).WithOne(x => x.Project).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Dossier>(entity =>
        {
            entity.ToTable("dossiers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(220);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => x.ProjectId);
        });

        modelBuilder.Entity<Source>(entity =>
        {
            entity.ToTable("sources");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.CollectionStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Platform).HasMaxLength(80);
            entity.Property(x => x.Url).HasMaxLength(2048);
            entity.HasMany(x => x.EvidenceItems).WithOne(x => x.Source).HasForeignKey(x => x.SourceId).OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => x.DossierId);
        });

        modelBuilder.Entity<EvidenceItem>(entity =>
        {
            entity.ToTable("evidence_items");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.ProvenanceStatus).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Title).HasMaxLength(220);
            entity.Property(x => x.StoragePath).HasMaxLength(1024);
            entity.Property(x => x.ContentHashSha256).HasMaxLength(128);
            entity.Property(x => x.PerceptualHash).HasMaxLength(256);
            entity.HasIndex(x => x.DossierId);
            entity.HasIndex(x => x.ContentHashSha256);
        });

        modelBuilder.Entity<AnalysisRun>(entity =>
        {
            entity.ToTable("analysis_runs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Pipeline).HasMaxLength(80);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => new { x.Status, x.StartedAt });
            entity.HasMany(x => x.Artifacts).WithOne(x => x.AnalysisRun).HasForeignKey(x => x.AnalysisRunId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AnalysisArtifact>(entity =>
        {
            entity.ToTable("analysis_artifacts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Filename).HasMaxLength(260);
            entity.Property(x => x.StorageKey).HasMaxLength(1024);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.Property(x => x.ArtifactType).HasMaxLength(80);
            entity.HasIndex(x => x.AnalysisRunId);
        });

        modelBuilder.Entity<Finding>(entity =>
        {
            entity.ToTable("findings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Category).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Confidence).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Direction).HasConversion<string>().HasMaxLength(60);
            entity.HasIndex(x => x.DossierId);
            entity.HasOne(x => x.EvidenceItem).WithMany(x => x.Findings).HasForeignKey(x => x.EvidenceItemId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.AnalysisRun).WithMany(x => x.Findings).HasForeignKey(x => x.AnalysisRunId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Claim>(entity =>
        {
            entity.ToTable("claims");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Confidence).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => x.DossierId);
        });

        modelBuilder.Entity<InvestigationTask>(entity =>
        {
            entity.ToTable("investigation_tasks");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Priority).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.TaskType).HasConversion<string>().HasMaxLength(60);
            entity.HasIndex(x => x.DossierId);
        });

        modelBuilder.Entity<ChainOfCustodyEvent>(entity =>
        {
            entity.ToTable("chain_of_custody_events");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EventType).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => x.EvidenceItemId);
        });

        modelBuilder.Entity<TimelineEntry>(entity =>
        {
            entity.ToTable("timeline_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Confidence).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Platform).HasMaxLength(80);
            entity.Property(x => x.Url).HasMaxLength(2048);
            entity.HasIndex(x => new { x.DossierId, x.Time });
        });

        modelBuilder.Entity<DossierEntity>(entity =>
        {
            entity.ToTable("dossier_entities");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasConversion<string>().HasMaxLength(60);
            entity.Property(x => x.Name).HasMaxLength(220);
            entity.Property(x => x.Handle).HasMaxLength(180);
            entity.Property(x => x.Platform).HasMaxLength(80);
            entity.Property(x => x.Url).HasMaxLength(2048);
            entity.Property(x => x.Confidence).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => x.DossierId);
        });

        modelBuilder.Entity<DossierEntityRelation>(entity =>
        {
            entity.ToTable("dossier_entity_relations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RelationType).HasMaxLength(120);
            entity.Property(x => x.Confidence).HasConversion<string>().HasMaxLength(40);
            entity.HasIndex(x => x.DossierId);
            entity.HasIndex(x => new { x.FromEntityId, x.ToEntityId, x.RelationType }).IsUnique();
            entity.HasOne(x => x.Dossier).WithMany(x => x.EntityRelations).HasForeignKey(x => x.DossierId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FromEntity).WithMany(x => x.OutgoingRelations).HasForeignKey(x => x.FromEntityId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.ToEntity).WithMany(x => x.IncomingRelations).HasForeignKey(x => x.ToEntityId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RobotsCacheEntry>(entity =>
        {
            entity.ToTable("robots_cache_entries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Host).HasMaxLength(260);
            entity.Property(x => x.UserAgent).HasMaxLength(180);
            entity.Property(x => x.Uri).HasMaxLength(2048);
            entity.HasIndex(x => new { x.Host, x.UserAgent, x.Uri });
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<Project>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Dossier>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<Claim>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<DossierEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<DossierEntityRelation>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
