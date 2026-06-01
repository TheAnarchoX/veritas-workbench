using Microsoft.EntityFrameworkCore;
using Veritas.Api.Endpoints;
using Veritas.Infrastructure;
using Veritas.Infrastructure.Persistence;
using Veritas.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("web", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .WithOrigins(
                "http://localhost:5173",
                "http://127.0.0.1:5173",
                "http://localhost:4173",
                "http://127.0.0.1:4173");
    });
});
builder.Services.AddVeritasInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("web");
app.MapVeritasEndpoints();

if (app.Configuration.GetValue("Database:ApplyMigrations", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VeritasDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Configuration.GetValue("Database:EnsureCreated", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VeritasDbContext>();
    await db.Database.EnsureCreatedAsync();
    await EnsureSqliteIncrementalSchemaAsync(db);
}

if (app.Configuration.GetValue("Demo:SeedData", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<VeritasDbContext>();
    await DemoSeeder.SeedAsync(db, CancellationToken.None);
}

static async Task EnsureSqliteIncrementalSchemaAsync(VeritasDbContext db)
{
    if (!string.Equals(db.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite", StringComparison.Ordinal))
    {
        return;
    }

    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "dossier_entities" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_dossier_entities" PRIMARY KEY,
            "DossierId" TEXT NOT NULL,
            "Kind" TEXT NOT NULL,
            "Name" TEXT NOT NULL,
            "Handle" TEXT NULL,
            "Platform" TEXT NULL,
            "Url" TEXT NULL,
            "Notes" TEXT NULL,
            "Confidence" TEXT NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL,
            CONSTRAINT "FK_dossier_entities_dossiers_DossierId" FOREIGN KEY ("DossierId") REFERENCES "dossiers" ("Id") ON DELETE CASCADE
        );
        """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE INDEX IF NOT EXISTS "IX_dossier_entities_DossierId" ON "dossier_entities" ("DossierId");
        """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "dossier_entity_relations" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_dossier_entity_relations" PRIMARY KEY,
            "DossierId" TEXT NOT NULL,
            "FromEntityId" TEXT NOT NULL,
            "ToEntityId" TEXT NOT NULL,
            "RelationType" TEXT NOT NULL,
            "Confidence" TEXT NOT NULL,
            "EvidenceBasis" TEXT NULL,
            "Notes" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NOT NULL,
            CONSTRAINT "FK_dossier_entity_relations_dossiers_DossierId" FOREIGN KEY ("DossierId") REFERENCES "dossiers" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_dossier_entity_relations_dossier_entities_FromEntityId" FOREIGN KEY ("FromEntityId") REFERENCES "dossier_entities" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_dossier_entity_relations_dossier_entities_ToEntityId" FOREIGN KEY ("ToEntityId") REFERENCES "dossier_entities" ("Id") ON DELETE CASCADE
        );
        """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE INDEX IF NOT EXISTS "IX_dossier_entity_relations_DossierId" ON "dossier_entity_relations" ("DossierId");
        """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_dossier_entity_relations_FromEntityId_ToEntityId_RelationType" ON "dossier_entity_relations" ("FromEntityId", "ToEntityId", "RelationType");
        """);
}

app.Run();

public partial class Program;
