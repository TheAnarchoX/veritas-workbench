using Microsoft.EntityFrameworkCore;
using Veritas.Domain;
using Veritas.Domain.Entities;
using Veritas.Infrastructure.Persistence;

namespace Veritas.Infrastructure.Seed;

public static class DemoSeeder
{
    public static async Task SeedAsync(VeritasDbContext db, CancellationToken ct)
    {
        if (await db.Projects.AnyAsync(ct))
        {
            return;
        }

        var project = new Project
        {
            Name = "Demo - Suspected AI Persona",
            Description = "Synthetic, neutral demo data for a provenance-first investigation."
        };

        var dossier = new Dossier
        {
            Project = project,
            Title = "Example dossier",
            Summary = "Synthetic dossier for testing claims, tasks, source intake, and conservative findings.",
            Status = DossierStatus.Active
        };

        dossier.Claims.AddRange(
        [
            new Claim { Text = "The account uses AI-generated imagery" },
            new Claim { Text = "The media was reposted from another platform" },
            new Claim { Text = "The account is a coordinated persona" }
        ]);

        dossier.Tasks.AddRange(
        [
            new InvestigationTask { Title = "Provide original media", Description = "Upload the original file or platform original where available.", TaskType = InvestigationTaskType.ProvideOriginalMedia, Priority = Priority.High },
            new InvestigationTask { Title = "Extract video frames", Description = "Run the video worker on any uploaded video evidence.", TaskType = InvestigationTaskType.ExtractVideoFrames, Priority = Priority.Medium },
            new InvestigationTask { Title = "Reverse-search background crops", Description = "Use cropped background regions to search for earlier appearances.", TaskType = InvestigationTaskType.ReverseImageSearch, Priority = Priority.Medium }
        ]);

        db.Projects.Add(project);
        db.Dossiers.Add(dossier);
        await db.SaveChangesAsync(ct);
    }
}
