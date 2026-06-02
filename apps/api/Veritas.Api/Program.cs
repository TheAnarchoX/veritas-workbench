using Microsoft.EntityFrameworkCore;
using System.Net.Sockets;
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
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex) when (IsDatabaseUnavailable(ex))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Database unavailable",
            detail = "The API is running, but it cannot connect to the configured database. Start Postgres/Docker or switch the database provider before retrying."
        });
    }
});
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
    await db.Database.ExecuteSqlRawAsync("""
        CREATE TABLE IF NOT EXISTS "claim_evidence_links" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_claim_evidence_links" PRIMARY KEY,
            "ClaimId" TEXT NOT NULL,
            "EvidenceItemId" TEXT NOT NULL,
            "Stance" TEXT NOT NULL,
            "Note" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            CONSTRAINT "FK_claim_evidence_links_claims_ClaimId" FOREIGN KEY ("ClaimId") REFERENCES "claims" ("Id") ON DELETE CASCADE,
            CONSTRAINT "FK_claim_evidence_links_evidence_items_EvidenceItemId" FOREIGN KEY ("EvidenceItemId") REFERENCES "evidence_items" ("Id") ON DELETE CASCADE
        );
        """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE INDEX IF NOT EXISTS "IX_claim_evidence_links_ClaimId" ON "claim_evidence_links" ("ClaimId");
        """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE INDEX IF NOT EXISTS "IX_claim_evidence_links_EvidenceItemId" ON "claim_evidence_links" ("EvidenceItemId");
        """);
    await db.Database.ExecuteSqlRawAsync("""
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_claim_evidence_links_ClaimId_EvidenceItemId" ON "claim_evidence_links" ("ClaimId", "EvidenceItemId");
        """);
    if (!await SqliteColumnExistsAsync(db, "sources", "AuthorEntityId"))
    {
        await db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "sources" ADD COLUMN "AuthorEntityId" TEXT NULL;
            """);
    }
    await db.Database.ExecuteSqlRawAsync("""
        CREATE INDEX IF NOT EXISTS "IX_sources_AuthorEntityId" ON "sources" ("AuthorEntityId");
        """);
}

static async Task<bool> SqliteColumnExistsAsync(VeritasDbContext db, string tableName, string columnName)
{
    await using var command = db.Database.GetDbConnection().CreateCommand();
    command.CommandText = $"PRAGMA table_info(\"{tableName.Replace("\"", "\"\"")}\")";
    if (command.Connection?.State != System.Data.ConnectionState.Open)
    {
        await db.Database.OpenConnectionAsync();
    }

    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static bool IsDatabaseUnavailable(Exception exception)
{
    for (var current = exception; current is not null; current = current.InnerException!)
    {
        if (current is SocketException)
        {
            return true;
        }

        var typeName = current.GetType().FullName ?? current.GetType().Name;
        if (typeName.StartsWith("Npgsql.", StringComparison.Ordinal))
        {
            return true;
        }
    }

    return false;
}

app.Run();

public partial class Program;
