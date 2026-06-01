using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Veritas.Application.Analysis;
using Veritas.Application.Policies;
using Veritas.Application.Reports;
using Veritas.Application.Storage;
using Veritas.Domain;
using Veritas.Domain.Entities;
using Veritas.Infrastructure.Persistence;

namespace Veritas.Api.Endpoints;

public static class VeritasEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static WebApplication MapVeritasEndpoints(this WebApplication app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new { status = "ok", name = "Veritas Workbench" }));

        api.MapGet("/projects", async (VeritasDbContext db, CancellationToken ct) =>
        {
            var projects = await db.Projects.Include(x => x.Dossiers).ToListAsync(ct);
            return Results.Ok(projects.OrderByDescending(x => x.UpdatedAt).Select(ProjectDto.From));
        });

        api.MapPost("/projects", async (CreateProjectRequest request, VeritasDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { error = "Project name is required." });
            }

            var project = new Project { Name = request.Name.Trim(), Description = request.Description?.Trim() };
            db.Projects.Add(project);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/projects/{project.Id}", ProjectDto.From(project));
        });

        api.MapGet("/projects/{id:guid}", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var project = await db.Projects.Include(x => x.Dossiers).FirstOrDefaultAsync(x => x.Id == id, ct);
            return project is null ? Results.NotFound() : Results.Ok(new { project = ProjectDto.From(project), dossiers = project.Dossiers.Select(DossierDto.From) });
        });

        api.MapGet("/projects/{projectId:guid}/dossiers", async (Guid projectId, VeritasDbContext db, CancellationToken ct) =>
        {
            var dossiers = await db.Dossiers.Where(x => x.ProjectId == projectId).ToListAsync(ct);
            return Results.Ok(dossiers.OrderByDescending(x => x.UpdatedAt).Select(DossierDto.From));
        });

        api.MapPost("/projects/{projectId:guid}/dossiers", async (Guid projectId, CreateDossierRequest request, VeritasDbContext db, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(new { error = "Dossier title is required." });
            }

            var projectExists = await db.Projects.AnyAsync(x => x.Id == projectId, ct);
            if (!projectExists)
            {
                return Results.NotFound();
            }

            var dossier = new Dossier { ProjectId = projectId, Title = request.Title.Trim(), Summary = request.Summary?.Trim(), Status = DossierStatus.Active };
            db.Dossiers.Add(dossier);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/dossiers/{dossier.Id}", DossierDto.From(dossier));
        });

        api.MapGet("/dossiers/{id:guid}", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var dossier = await db.Dossiers
                .Include(x => x.Sources)
                .Include(x => x.EvidenceItems).ThenInclude(x => x.AnalysisRuns).ThenInclude(x => x.Artifacts)
                .Include(x => x.Findings)
                .Include(x => x.Claims)
                .Include(x => x.Tasks)
                .Include(x => x.TimelineEntries)
                .Include(x => x.Entities)
                .Include(x => x.EntityRelations)
                .FirstOrDefaultAsync(x => x.Id == id, ct);

            return dossier is null
                ? Results.NotFound()
                : Results.Ok(new
                {
                    dossier = DossierDto.From(dossier),
                    sources = dossier.Sources.OrderByDescending(x => x.ObservedAt).Select(SourceDto.From),
                    evidence = dossier.EvidenceItems.OrderByDescending(x => x.CreatedAt).Select(EvidenceDto.From),
                    findings = dossier.Findings.OrderByDescending(x => x.CreatedAt).Select(FindingDto.From),
                    claims = dossier.Claims.OrderBy(x => x.CreatedAt).Select(ClaimDto.From),
                    tasks = dossier.Tasks.OrderByDescending(x => x.Priority).ThenBy(x => x.CreatedAt).Select(InvestigationTaskDto.From),
                    timeline = dossier.TimelineEntries.OrderBy(x => x.Time).Select(TimelineEntryDto.From),
                    entities = dossier.Entities.OrderBy(x => x.Kind).ThenBy(x => x.Name).Select(DossierEntityDto.From),
                    entityRelations = dossier.EntityRelations.OrderBy(x => x.RelationType).ThenBy(x => x.CreatedAt).Select(DossierEntityRelationDto.From)
                });
        });

        api.MapPost("/dossiers/{id:guid}/sources/url", AddUrlSourceAsync);
        api.MapPatch("/sources/{id:guid}", PatchSourceAsync);
        api.MapDelete("/sources/{id:guid}", DeleteSourceAsync);
        api.MapGet("/dossiers/{id:guid}/sources", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var sources = await db.Sources.Where(x => x.DossierId == id).ToListAsync(ct);
            return Results.Ok(sources.OrderByDescending(x => x.ObservedAt).Select(SourceDto.From));
        });

        api.MapPost("/dossiers/{id:guid}/evidence/upload", UploadEvidenceAsync)
            .DisableAntiforgery();
        api.MapPost("/dossiers/{id:guid}/text/triage", TriageTextAsync);
        api.MapGet("/dossiers/{id:guid}/evidence", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var evidence = await db.EvidenceItems
                .Include(x => x.AnalysisRuns).ThenInclude(x => x.Artifacts)
                .Where(x => x.DossierId == id)
                .ToListAsync(ct);
            return Results.Ok(evidence.OrderByDescending(x => x.CreatedAt).Select(EvidenceDto.From));
        });
        api.MapGet("/evidence/{id:guid}", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var item = await db.EvidenceItems
                .Include(x => x.AnalysisRuns).ThenInclude(x => x.Artifacts)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return item is null ? Results.NotFound() : Results.Ok(EvidenceDto.From(item));
        });
        api.MapGet("/evidence/{id:guid}/file", DownloadEvidenceAsync);
        api.MapPatch("/evidence/{id:guid}", PatchEvidenceAsync);
        api.MapDelete("/evidence/{id:guid}", DeleteEvidenceAsync);

        api.MapPost("/evidence/{id:guid}/analysis/image", async (Guid id, IAnalysisService analysis, CancellationToken ct) =>
        {
            try
            {
                var run = await analysis.QueueAnalysisAsync(id, "image", ct);
                return Results.Accepted($"/api/evidence/{id}/analysis-runs", AnalysisRunDto.From(run));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        api.MapPost("/evidence/{id:guid}/analysis/video", async (Guid id, IAnalysisService analysis, CancellationToken ct) =>
        {
            try
            {
                var run = await analysis.QueueAnalysisAsync(id, "video", ct);
                return Results.Accepted($"/api/evidence/{id}/analysis-runs", AnalysisRunDto.From(run));
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        api.MapGet("/evidence/{id:guid}/analysis-runs", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var runs = await db.AnalysisRuns.Include(x => x.Artifacts).Where(x => x.EvidenceItemId == id).ToListAsync(ct);
            return Results.Ok(runs.OrderByDescending(x => x.StartedAt).Select(AnalysisRunDto.From));
        });
        api.MapGet("/analysis-artifacts/{id:guid}", DownloadArtifactAsync);
        api.MapGet("/analysis-artifacts/{id:guid}/view", ViewArtifactAsync);
        api.MapGet("/analysis-runs/{id:guid}/artifacts.zip", DownloadArtifactsZipAsync);

        api.MapGet("/dossiers/{id:guid}/findings", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var findings = await db.Findings.Where(x => x.DossierId == id).ToListAsync(ct);
            return Results.Ok(findings.OrderByDescending(x => x.CreatedAt).Select(FindingDto.From));
        });
        api.MapPost("/dossiers/{id:guid}/findings", CreateFindingAsync);
        api.MapPatch("/findings/{id:guid}", PatchFindingAsync);
        api.MapDelete("/findings/{id:guid}", DeleteFindingAsync);

        api.MapGet("/dossiers/{id:guid}/claims", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var claims = await db.Claims.Where(x => x.DossierId == id).ToListAsync(ct);
            return Results.Ok(claims.OrderBy(x => x.CreatedAt).Select(ClaimDto.From));
        });
        api.MapPost("/dossiers/{id:guid}/claims", CreateClaimAsync);
        api.MapPatch("/claims/{id:guid}", PatchClaimAsync);
        api.MapDelete("/claims/{id:guid}", DeleteClaimAsync);

        api.MapGet("/dossiers/{id:guid}/tasks", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var tasks = await db.InvestigationTasks.Where(x => x.DossierId == id).ToListAsync(ct);
            return Results.Ok(tasks.OrderByDescending(x => x.Priority).ThenBy(x => x.CreatedAt).Select(InvestigationTaskDto.From));
        });
        api.MapPost("/dossiers/{id:guid}/tasks", CreateTaskAsync);
        api.MapPatch("/tasks/{id:guid}", PatchTaskAsync);
        api.MapDelete("/tasks/{id:guid}", DeleteTaskAsync);

        api.MapGet("/dossiers/{id:guid}/timeline", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var entries = await db.TimelineEntries.Where(x => x.DossierId == id).ToListAsync(ct);
            return Results.Ok(entries.OrderBy(x => x.Time).Select(TimelineEntryDto.From));
        });
        api.MapPost("/dossiers/{id:guid}/timeline", CreateTimelineEntryAsync);
        api.MapPatch("/timeline/{id:guid}", PatchTimelineEntryAsync);
        api.MapDelete("/timeline/{id:guid}", DeleteTimelineEntryAsync);

        api.MapGet("/dossiers/{id:guid}/entities", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var entities = await db.DossierEntities.Where(x => x.DossierId == id).ToListAsync(ct);
            return Results.Ok(entities.OrderBy(x => x.Kind).ThenBy(x => x.Name).Select(DossierEntityDto.From));
        });
        api.MapPost("/dossiers/{id:guid}/entities", CreateDossierEntityAsync);
        api.MapPatch("/entities/{id:guid}", PatchDossierEntityAsync);
        api.MapDelete("/entities/{id:guid}", DeleteDossierEntityAsync);
        api.MapGet("/dossiers/{id:guid}/entity-relations", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var relations = await db.DossierEntityRelations.Where(x => x.DossierId == id).ToListAsync(ct);
            return Results.Ok(relations.OrderBy(x => x.RelationType).ThenBy(x => x.CreatedAt).Select(DossierEntityRelationDto.From));
        });
        api.MapPost("/dossiers/{id:guid}/entity-relations", CreateDossierEntityRelationAsync);
        api.MapPatch("/entity-relations/{id:guid}", PatchDossierEntityRelationAsync);
        api.MapDelete("/entity-relations/{id:guid}", DeleteDossierEntityRelationAsync);

        api.MapGet("/dossiers/{id:guid}/report/markdown", async (Guid id, IReportService reports, CancellationToken ct) =>
        {
            try
            {
                var markdown = await reports.GenerateDossierMarkdownAsync(id, ct);
                return Results.Text(markdown, "text/markdown");
            }
            catch (InvalidOperationException)
            {
                return Results.NotFound();
            }
        });

        api.MapGet("/settings", (IConfiguration configuration) => Results.Ok(new
        {
            name = "Veritas Workbench",
            collectionPolicy = "robots.txt-aware, API-first, manual fallback when blocked or ambiguous",
            userAgent = configuration["Robots:UserAgent"] ?? "VeritasWorkbench/0.1",
            ethicalBoundaries = new[]
            {
                "No private-person face recognition",
                "No harassment, doxxing, stalking, or deanonymization workflows",
                "Findings require confidence, evidence, limitations, and falsification paths"
            }
        }));

        return app;
    }

    private static async Task<IResult> AddUrlSourceAsync(
        Guid id,
        AddUrlSourceRequest request,
        VeritasDbContext db,
        IRobotsPolicyService robots,
        IConfiguration configuration,
        CancellationToken ct)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
        {
            return Results.BadRequest(new { error = "A valid absolute URL is required." });
        }

        var dossierExists = await db.Dossiers.AnyAsync(x => x.Id == id, ct);
        if (!dossierExists)
        {
            return Results.NotFound();
        }

        var platform = GuessPlatform(uri);
        var userAgent = configuration["Robots:UserAgent"] ?? "VeritasWorkbench/0.1";
        var decision = await robots.CanFetchAsync(uri, userAgent, ct);
        var requiresManual = IsTermsSensitivePlatform(platform) || !decision.Allowed;

        var source = new Source
        {
            DossierId = id,
            Type = SourceType.Url,
            Url = uri.ToString(),
            Platform = platform,
            Title = request.Title,
            AuthorHandle = request.AuthorHandle,
            ObservedAt = request.ObservedAt ?? DateTimeOffset.UtcNow,
            CollectionStatus = requiresManual
                ? decision.Allowed ? CollectionStatus.RequiresManualInput : CollectionStatus.BlockedByRobots
                : CollectionStatus.Allowed,
            RobotsDecision = JsonSerializer.Serialize(decision, JsonOptions),
            Notes = requiresManual
                ? "Automatic collection is not configured or not clearly allowed. Provide the original media file, platform-original media URL, archive capture, screenshot, or metadata text manually."
                : "robots.txt did not block this URL. Fetching should still respect platform terms, rate limits, and API-first collection."
        };

        db.Sources.Add(source);

        if (requiresManual)
        {
            db.InvestigationTasks.Add(new InvestigationTask
            {
                DossierId = id,
                Title = platform is "X" ? "Provide original media file or pbs.twimg.com name=orig URL" : "Provide original media or archive capture",
                Description = "Open the media in its original context, prefer the original file or platform media URL over screenshots, and upload screenshots only when originals are unavailable.",
                Priority = Priority.High,
                TaskType = InvestigationTaskType.ProvideOriginalMedia
            });
        }

        db.Findings.Add(new Finding
        {
            DossierId = id,
            Category = FindingCategory.RobotsPolicy,
            Claim = decision.Allowed ? "Collection policy was checked for the submitted URL." : "Automatic fetching is not allowed or not clearly available for this URL.",
            Confidence = ConfidenceLevel.High,
            Direction = FindingDirection.Neutral,
            Evidence = decision.Reason,
            Limitations = "robots.txt and local policy checks do not grant rights to bypass login walls, anti-bot systems, paywalls, or platform terms.",
            FalsificationPath = "Configure an official API route or manually provide original media, metadata, or an archive link."
        });

        AddWorkflowTimeline(
            db,
            id,
            $"Source intake: {source.Title ?? source.Url}",
            source.Title ?? source.AuthorHandle ?? source.Url,
            source.Url,
            ConfidenceLevel.Medium,
            source.Notes,
            source.ObservedAt,
            source.Platform,
            firstKnownAppearance: source.FirstSeenAt == source.ObservedAt);

        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/dossiers/{id}/sources", SourceDto.From(source));
    }

    private static async Task<IResult> PatchSourceAsync(Guid id, PatchSourceRequest request, VeritasDbContext db, CancellationToken ct)
    {
        var source = await db.Sources.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (source is null)
        {
            return Results.NotFound();
        }

        if (request.Title is not null) source.Title = request.Title.Trim();
        if (request.AuthorHandle is not null) source.AuthorHandle = request.AuthorHandle.Trim();
        if (request.Platform is not null) source.Platform = request.Platform.Trim();
        if (request.Notes is not null) source.Notes = request.Notes.Trim();
        if (!string.IsNullOrWhiteSpace(request.CollectionStatus)) source.CollectionStatus = ParseEnum(request.CollectionStatus, source.CollectionStatus);
        if (request.ObservedAt is not null) source.ObservedAt = request.ObservedAt;
        if (request.FirstSeenAt is not null) source.FirstSeenAt = request.FirstSeenAt;

        await db.SaveChangesAsync(ct);
        return Results.Ok(SourceDto.From(source));
    }

    private static async Task<IResult> DeleteSourceAsync(Guid id, VeritasDbContext db, CancellationToken ct)
    {
        var source = await db.Sources.Include(x => x.EvidenceItems).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (source is null)
        {
            return Results.NotFound();
        }

        foreach (var evidence in source.EvidenceItems)
        {
            evidence.SourceId = null;
        }

        db.Sources.Remove(source);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> UploadEvidenceAsync(Guid id, HttpRequest request, VeritasDbContext db, IEvidenceStorage storage, CancellationToken ct)
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest(new { error = "multipart/form-data is required." });
        }

        var dossierExists = await db.Dossiers.AnyAsync(x => x.Id == id, ct);
        if (!dossierExists)
        {
            return Results.NotFound();
        }

        var form = await request.ReadFormAsync(ct);
        var file = form.Files["file"];
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { error = "A non-empty file is required." });
        }

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        await using var saveStream = new MemoryStream(bytes, writable: false);
        var stored = await storage.SaveAsync(saveStream, file.FileName, string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType, ct);

        var sourceId = TryParseGuid(form["sourceId"].FirstOrDefault());
        var type = GuessEvidenceType(file.ContentType, file.FileName, form["type"].FirstOrDefault());
        var provenance = ParseEnum(form["provenanceStatus"].FirstOrDefault(), ProvenanceStatus.Unknown);

        var evidence = new EvidenceItem
        {
            DossierId = id,
            SourceId = sourceId,
            Type = type,
            Title = NonBlank(form["title"].FirstOrDefault(), Path.GetFileNameWithoutExtension(file.FileName)),
            Description = form["description"].FirstOrDefault(),
            OriginalFilename = file.FileName,
            ContentHashSha256 = hash,
            StoragePath = stored.StorageKey,
            MimeType = stored.ContentType,
            FileSizeBytes = stored.SizeBytes,
            ProvenanceStatus = provenance
        };

        db.EvidenceItems.Add(evidence);
        db.ChainOfCustodyEvents.Add(new ChainOfCustodyEvent
        {
            EvidenceItemId = evidence.Id,
            EventType = CustodyEventType.Uploaded,
            Actor = "local-user",
            DetailsJson = JsonSerializer.Serialize(new { stored.OriginalFilename, stored.StorageKey })
        });
        db.ChainOfCustodyEvents.Add(new ChainOfCustodyEvent
        {
            EvidenceItemId = evidence.Id,
            EventType = CustodyEventType.Hashed,
            Actor = "system",
            DetailsJson = JsonSerializer.Serialize(new { sha256 = hash })
        });

        AddSuggestedTasks(db, id, type, provenance);
        Source? linkedSource = null;
        if (sourceId is not null)
        {
            linkedSource = await db.Sources.FirstOrDefaultAsync(x => x.Id == sourceId && x.DossierId == id, ct);
            if (linkedSource is not null)
            {
                linkedSource.CollectionStatus = CollectionStatus.Collected;
            }
        }

        AddWorkflowTimeline(
            db,
            id,
            $"Evidence preserved: {evidence.Title}",
            evidence.Title,
            linkedSource?.Url,
            provenance is ProvenanceStatus.OriginalProvided or ProvenanceStatus.PlatformOriginal ? ConfidenceLevel.High : ConfidenceLevel.Medium,
            linkedSource is null ? "Uploaded without a linked source." : $"Linked to source {linkedSource.Title ?? linkedSource.Url}.",
            evidence.CapturedAt,
            linkedSource?.Platform,
            evidence.ContentHashSha256);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/evidence/{evidence.Id}", EvidenceDto.From(evidence));
    }

    private static async Task<IResult> TriageTextAsync(Guid id, TextTriageRequest request, VeritasDbContext db, IEvidenceStorage storage, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.BadRequest(new { error = "Text is required." });
        }

        var dossierExists = await db.Dossiers.AnyAsync(x => x.Id == id, ct);
        if (!dossierExists)
        {
            return Results.NotFound();
        }

        var title = NonBlank(request.Title, "Text sample");
        var bytes = Encoding.UTF8.GetBytes(request.Text);
        await using var input = new MemoryStream(bytes, writable: false);
        var stored = await storage.SaveAsync(input, $"{SanitizeTitle(title)}.txt", "text/plain", ct);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var signals = DetectTextSignals(request.Text);
        var signalText = signals.Count == 0 ? "No strong stylometric or boilerplate signals were triggered by this lightweight triage." : string.Join("; ", signals);

        var evidence = new EvidenceItem
        {
            DossierId = id,
            SourceId = request.SourceId,
            Type = EvidenceType.Text,
            Title = title,
            Description = "Text sample submitted for cautious AI-style triage.",
            OriginalFilename = stored.OriginalFilename,
            ContentHashSha256 = hash,
            StoragePath = stored.StorageKey,
            MimeType = stored.ContentType,
            FileSizeBytes = stored.SizeBytes,
            ProvenanceStatus = ProvenanceStatus.Unknown
        };

        var finding = new Finding
        {
            DossierId = id,
            EvidenceItemId = evidence.Id,
            Category = FindingCategory.ManualObservation,
            Claim = signals.Count == 0
                ? "Lightweight text triage did not find strong AI-style boilerplate signals."
                : "Lightweight text triage found AI-style or low-specificity writing signals that need manual review.",
            Confidence = signals.Count >= 4 ? ConfidenceLevel.Medium : ConfidenceLevel.Low,
            Direction = signals.Count == 0 ? FindingDirection.Neutral : FindingDirection.Inconclusive,
            Evidence = signalText,
            Limitations = "Stylometric cues are weak signals. Edited human text, templates, translations, corporate style guides, and accessibility rewrites can produce similar patterns.",
            FalsificationPath = "Compare with known writing by the same account, platform edit history, drafts, timestamps, and source-specific context."
        };

        var task = new InvestigationTask
        {
            DossierId = id,
            Title = "Manually review text authenticity signals",
            Description = "Compare this sample against known writing from the same source before using style as evidence.",
            Priority = signals.Count >= 4 ? Priority.High : Priority.Medium,
            TaskType = InvestigationTaskType.TextAuthenticityReview
        };

        var timelineEntry = new TimelineEntry
        {
            DossierId = id,
            Time = request.ObservedAt ?? DateTimeOffset.UtcNow,
            Platform = request.Platform,
            Url = request.Url,
            Source = title,
            EvidenceHash = hash,
            Caption = $"Text triage: {title}",
            Notes = signalText,
            Confidence = ConfidenceLevel.Low
        };

        db.EvidenceItems.Add(evidence);
        db.Findings.Add(finding);
        db.InvestigationTasks.Add(task);
        db.TimelineEntries.Add(timelineEntry);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/evidence/{evidence.Id}", new TextTriageResultDto(
            EvidenceDto.From(evidence),
            FindingDto.From(finding),
            new[] { InvestigationTaskDto.From(task) },
            TimelineEntryDto.From(timelineEntry),
            signals));
    }

    private static async Task<IResult> DownloadEvidenceAsync(Guid id, VeritasDbContext db, IEvidenceStorage storage, CancellationToken ct)
    {
        var item = await db.EvidenceItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null)
        {
            return Results.NotFound();
        }

        var stream = await storage.OpenReadAsync(item.StoragePath, ct);
        return Results.Stream(stream, item.MimeType ?? "application/octet-stream", item.OriginalFilename ?? $"{item.Id}.bin");
    }

    private static async Task<IResult> PatchEvidenceAsync(Guid id, PatchEvidenceRequest request, VeritasDbContext db, CancellationToken ct)
    {
        var item = await db.EvidenceItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null)
        {
            return Results.NotFound();
        }

        if (request.ClearSource == true)
        {
            item.SourceId = null;
        }
        else if (request.SourceId is not null)
        {
            var sourceExists = await db.Sources.AnyAsync(x => x.Id == request.SourceId && x.DossierId == item.DossierId, ct);
            if (!sourceExists)
            {
                return Results.BadRequest(new { error = "Linked source must belong to the same dossier." });
            }

            item.SourceId = request.SourceId;
        }

        if (request.Title is not null) item.Title = NonBlank(request.Title, item.Title);
        if (request.Description is not null) item.Description = request.Description.Trim();
        if (!string.IsNullOrWhiteSpace(request.Type)) item.Type = ParseEnum(request.Type, item.Type);
        if (!string.IsNullOrWhiteSpace(request.ProvenanceStatus)) item.ProvenanceStatus = ParseEnum(request.ProvenanceStatus, item.ProvenanceStatus);
        if (request.CapturedAt is not null) item.CapturedAt = request.CapturedAt;

        await db.SaveChangesAsync(ct);
        return Results.Ok(EvidenceDto.From(item));
    }

    private static async Task<IResult> DeleteEvidenceAsync(Guid id, VeritasDbContext db, CancellationToken ct)
    {
        var item = await db.EvidenceItems.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null)
        {
            return Results.NotFound();
        }

        db.EvidenceItems.Remove(item);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DownloadArtifactAsync(Guid id, VeritasDbContext db, IEvidenceStorage storage, CancellationToken ct)
    {
        var artifact = await db.AnalysisArtifacts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (artifact is null)
        {
            return Results.NotFound();
        }

        var stream = await storage.OpenReadAsync(artifact.StorageKey, ct);
        return Results.Stream(stream, artifact.ContentType ?? "application/octet-stream", artifact.Filename);
    }

    private static async Task<IResult> ViewArtifactAsync(Guid id, VeritasDbContext db, IEvidenceStorage storage, CancellationToken ct)
    {
        var artifact = await db.AnalysisArtifacts.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (artifact is null)
        {
            return Results.NotFound();
        }

        var stream = await storage.OpenReadAsync(artifact.StorageKey, ct);
        return Results.Stream(stream, artifact.ContentType ?? "application/octet-stream");
    }

    private static async Task<IResult> DownloadArtifactsZipAsync(Guid id, VeritasDbContext db, IEvidenceStorage storage, CancellationToken ct)
    {
        var run = await db.AnalysisRuns.Include(x => x.Artifacts).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (run is null)
        {
            return Results.NotFound();
        }

        if (run.Artifacts.Count == 0)
        {
            return Results.BadRequest(new { error = "This analysis run has no artifacts." });
        }

        await using var zipStream = new MemoryStream();
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var artifact in run.Artifacts.OrderBy(x => x.Filename))
            {
                var entry = archive.CreateEntry(UniqueZipName(artifact.Filename, usedNames), CompressionLevel.Fastest);
                await using var entryStream = entry.Open();
                await using var artifactStream = await storage.OpenReadAsync(artifact.StorageKey, ct);
                await artifactStream.CopyToAsync(entryStream, ct);
            }
        }

        return Results.File(zipStream.ToArray(), "application/zip", $"analysis-run-{run.Id:N}-artifacts.zip");
    }

    private static async Task<IResult> CreateFindingAsync(Guid id, CreateFindingRequest request, VeritasDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Claim))
        {
            return Results.BadRequest(new { error = "Finding claim is required." });
        }

        var exists = await db.Dossiers.AnyAsync(x => x.Id == id, ct);
        if (!exists)
        {
            return Results.NotFound();
        }

        if (!await FindingLinksBelongToDossierAsync(id, request.EvidenceItemId, request.AnalysisRunId, db, ct))
        {
            return Results.BadRequest(new { error = "Linked evidence and analysis runs must belong to the same dossier." });
        }

        var finding = new Finding
        {
            DossierId = id,
            EvidenceItemId = request.EvidenceItemId,
            AnalysisRunId = request.AnalysisRunId,
            Category = ParseEnum(request.Category, FindingCategory.Other),
            Claim = request.Claim.Trim(),
            Confidence = ParseEnum(request.Confidence, ConfidenceLevel.None),
            Direction = ParseEnum(request.Direction, FindingDirection.Inconclusive),
            Evidence = NonBlank(request.Evidence, "Manual observation; evidence basis should be expanded before publication."),
            Limitations = NonBlank(request.Limitations, "Manual findings require independent support and should not be used for attribution alone."),
            FalsificationPath = NonBlank(request.FalsificationPath, "Provide original media or contradictory chronology.")
        };

        db.Findings.Add(finding);
        AddWorkflowTimeline(db, id, $"Finding recorded: {ShortText(finding.Claim)}", "Finding", null, finding.Confidence, finding.Evidence);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/dossiers/{id}/findings", FindingDto.From(finding));
    }

    private static async Task<IResult> PatchFindingAsync(Guid id, PatchFindingRequest request, VeritasDbContext db, CancellationToken ct)
    {
        var finding = await db.Findings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (finding is null)
        {
            return Results.NotFound();
        }

        var previousConfidence = finding.Confidence;
        var previousDirection = finding.Direction;

        if (!await FindingLinksBelongToDossierAsync(finding.DossierId, request.EvidenceItemId, request.AnalysisRunId, db, ct))
        {
            return Results.BadRequest(new { error = "Linked evidence and analysis runs must belong to the same dossier." });
        }

        if (request.EvidenceItemId is not null) finding.EvidenceItemId = request.EvidenceItemId;
        if (request.AnalysisRunId is not null) finding.AnalysisRunId = request.AnalysisRunId;
        if (!string.IsNullOrWhiteSpace(request.Category)) finding.Category = ParseEnum(request.Category, finding.Category);
        if (!string.IsNullOrWhiteSpace(request.Claim)) finding.Claim = request.Claim.Trim();
        if (!string.IsNullOrWhiteSpace(request.Confidence)) finding.Confidence = ParseEnum(request.Confidence, finding.Confidence);
        if (!string.IsNullOrWhiteSpace(request.Direction)) finding.Direction = ParseEnum(request.Direction, finding.Direction);
        if (request.Evidence is not null) finding.Evidence = NonBlank(request.Evidence, finding.Evidence);
        if (request.Limitations is not null) finding.Limitations = NonBlank(request.Limitations, finding.Limitations);
        if (request.FalsificationPath is not null) finding.FalsificationPath = NonBlank(request.FalsificationPath, finding.FalsificationPath);

        if (previousConfidence != finding.Confidence || previousDirection != finding.Direction)
        {
            AddWorkflowTimeline(
                db,
                finding.DossierId,
                $"Finding updated: {ShortText(finding.Claim)}",
                "Finding",
                null,
                finding.Confidence,
                $"Direction {previousDirection} -> {finding.Direction}; confidence {previousConfidence} -> {finding.Confidence}.");
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(FindingDto.From(finding));
    }

    private static async Task<IResult> DeleteFindingAsync(Guid id, VeritasDbContext db, CancellationToken ct)
    {
        var finding = await db.Findings.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (finding is null)
        {
            return Results.NotFound();
        }

        db.Findings.Remove(finding);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateClaimAsync(Guid id, CreateClaimRequest request, VeritasDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return Results.BadRequest(new { error = "Claim text is required." });
        }

        var exists = await db.Dossiers.AnyAsync(x => x.Id == id, ct);
        if (!exists)
        {
            return Results.NotFound();
        }

        var claim = new Claim
        {
            DossierId = id,
            Text = request.Text.Trim(),
            Status = ParseEnum(request.Status, ClaimStatus.Unassessed),
            Confidence = ParseEnum(request.Confidence, ConfidenceLevel.None),
            Rationale = request.Rationale
        };

        db.Claims.Add(claim);
        AddWorkflowTimeline(db, id, $"Claim opened: {ShortText(claim.Text)}", "Claim", null, claim.Confidence, claim.Rationale);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/dossiers/{id}/claims", ClaimDto.From(claim));
    }

    private static async Task<IResult> PatchClaimAsync(Guid id, PatchClaimRequest request, VeritasDbContext db, CancellationToken ct)
    {
        var claim = await db.Claims.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (claim is null)
        {
            return Results.NotFound();
        }

        var previousStatus = claim.Status;
        var previousConfidence = claim.Confidence;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            claim.Status = ParseEnum(request.Status, claim.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.Text))
        {
            claim.Text = request.Text.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Confidence))
        {
            claim.Confidence = ParseEnum(request.Confidence, claim.Confidence);
        }

        if (request.Rationale is not null)
        {
            claim.Rationale = request.Rationale;
        }

        if (previousStatus != claim.Status || previousConfidence != claim.Confidence)
        {
            AddWorkflowTimeline(
                db,
                claim.DossierId,
                $"Claim updated: {ShortText(claim.Text)}",
                "Claim",
                null,
                claim.Confidence,
                $"Status {previousStatus} -> {claim.Status}; confidence {previousConfidence} -> {claim.Confidence}.");
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(ClaimDto.From(claim));
    }

    private static async Task<IResult> DeleteClaimAsync(Guid id, VeritasDbContext db, CancellationToken ct)
    {
        var claim = await db.Claims.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (claim is null)
        {
            return Results.NotFound();
        }

        db.Claims.Remove(claim);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateTaskAsync(Guid id, CreateTaskRequest request, VeritasDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new { error = "Task title is required." });
        }

        var exists = await db.Dossiers.AnyAsync(x => x.Id == id, ct);
        if (!exists)
        {
            return Results.NotFound();
        }

        var task = new InvestigationTask
        {
            DossierId = id,
            Title = request.Title.Trim(),
            Description = request.Description,
            Priority = ParseEnum(request.Priority, Priority.Medium),
            TaskType = ParseEnum(request.TaskType, InvestigationTaskType.Other)
        };

        db.InvestigationTasks.Add(task);
        AddWorkflowTimeline(db, id, $"Task opened: {ShortText(task.Title)}", "Task", null, ConfidenceLevel.Low, task.Description);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/dossiers/{id}/tasks", InvestigationTaskDto.From(task));
    }

    private static async Task<IResult> PatchTaskAsync(Guid id, PatchTaskRequest request, VeritasDbContext db, CancellationToken ct)
    {
        var task = await db.InvestigationTasks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (task is null)
        {
            return Results.NotFound();
        }

        var previousStatus = task.Status;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            task.Status = ParseEnum(request.Status, task.Status);
            task.CompletedAt = task.Status == InvestigationTaskStatus.Done ? DateTimeOffset.UtcNow : null;
        }

        if (!string.IsNullOrWhiteSpace(request.Title))
        {
            task.Title = request.Title.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            task.Priority = ParseEnum(request.Priority, task.Priority);
        }

        if (!string.IsNullOrWhiteSpace(request.TaskType))
        {
            task.TaskType = ParseEnum(request.TaskType, task.TaskType);
        }

        if (request.Description is not null)
        {
            task.Description = request.Description;
        }

        if (previousStatus != task.Status)
        {
            AddWorkflowTimeline(
                db,
                task.DossierId,
                $"Task {task.Status}: {ShortText(task.Title)}",
                "Task",
                null,
                task.Status is InvestigationTaskStatus.Done ? ConfidenceLevel.Medium : ConfidenceLevel.Low,
                task.Description);
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(InvestigationTaskDto.From(task));
    }

    private static async Task<IResult> DeleteTaskAsync(Guid id, VeritasDbContext db, CancellationToken ct)
    {
        var task = await db.InvestigationTasks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (task is null)
        {
            return Results.NotFound();
        }

        db.InvestigationTasks.Remove(task);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateTimelineEntryAsync(Guid id, CreateTimelineEntryRequest request, VeritasDbContext db, CancellationToken ct)
    {
        var exists = await db.Dossiers.AnyAsync(x => x.Id == id, ct);
        if (!exists)
        {
            return Results.NotFound();
        }

        var entry = new TimelineEntry
        {
            DossierId = id,
            Time = request.Time ?? DateTimeOffset.UtcNow,
            Platform = request.Platform,
            Url = request.Url,
            Source = request.Source,
            EvidenceHash = request.EvidenceHash,
            Caption = request.Caption,
            FirstKnownAppearance = request.FirstKnownAppearance ?? false,
            Notes = request.Notes,
            Confidence = ParseEnum(request.Confidence, ConfidenceLevel.None)
        };

        db.TimelineEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/dossiers/{id}/timeline", TimelineEntryDto.From(entry));
    }

    private static async Task<IResult> PatchTimelineEntryAsync(Guid id, PatchTimelineEntryRequest request, VeritasDbContext db, CancellationToken ct)
    {
        var entry = await db.TimelineEntries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entry is null)
        {
            return Results.NotFound();
        }

        if (request.Time is not null) entry.Time = request.Time.Value;
        if (request.Platform is not null) entry.Platform = request.Platform.Trim();
        if (request.Url is not null) entry.Url = request.Url.Trim();
        if (request.Source is not null) entry.Source = request.Source.Trim();
        if (request.EvidenceHash is not null) entry.EvidenceHash = request.EvidenceHash.Trim();
        if (request.Caption is not null) entry.Caption = request.Caption.Trim();
        if (request.FirstKnownAppearance is not null) entry.FirstKnownAppearance = request.FirstKnownAppearance.Value;
        if (request.Notes is not null) entry.Notes = request.Notes.Trim();
        if (!string.IsNullOrWhiteSpace(request.Confidence)) entry.Confidence = ParseEnum(request.Confidence, entry.Confidence);

        await db.SaveChangesAsync(ct);
        return Results.Ok(TimelineEntryDto.From(entry));
    }

    private static async Task<IResult> DeleteTimelineEntryAsync(Guid id, VeritasDbContext db, CancellationToken ct)
    {
        var entry = await db.TimelineEntries.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entry is null)
        {
            return Results.NotFound();
        }

        db.TimelineEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateDossierEntityAsync(Guid id, CreateDossierEntityRequest request, VeritasDbContext db, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "Entity name is required." });
        }

        var exists = await db.Dossiers.AnyAsync(x => x.Id == id, ct);
        if (!exists)
        {
            return Results.NotFound();
        }

        var entity = new DossierEntity
        {
            DossierId = id,
            Kind = ParseEnum(request.Kind, DossierEntityKind.Other),
            Name = request.Name.Trim(),
            Handle = request.Handle?.Trim(),
            Platform = request.Platform?.Trim(),
            Url = request.Url?.Trim(),
            Notes = request.Notes?.Trim(),
            Confidence = ParseEnum(request.Confidence, ConfidenceLevel.None)
        };

        db.DossierEntities.Add(entity);
        AddWorkflowTimeline(
            db,
            id,
            $"Entity added: {ShortText(entity.Name)}",
            entity.Handle ?? entity.Name,
            entity.Url,
            entity.Confidence,
            entity.Notes,
            platform: entity.Platform);
        if (!string.IsNullOrWhiteSpace(entity.Handle) || !string.IsNullOrWhiteSpace(entity.Url))
        {
            db.InvestigationTasks.Add(new InvestigationTask
            {
                DossierId = id,
                Title = $"Build account timeline for {entity.Handle ?? entity.Name}",
                Description = "Collect profile metadata, archived snapshots, first-seen dates, and cross-platform aliases before drawing identity or coordination conclusions.",
                Priority = Priority.Medium,
                TaskType = InvestigationTaskType.AccountTimeline
            });
        }

        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/dossiers/{id}/entities", DossierEntityDto.From(entity));
    }

    private static async Task<IResult> PatchDossierEntityAsync(Guid id, PatchDossierEntityRequest request, VeritasDbContext db, CancellationToken ct)
    {
        var entity = await db.DossierEntities.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return Results.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(request.Kind)) entity.Kind = ParseEnum(request.Kind, entity.Kind);
        if (!string.IsNullOrWhiteSpace(request.Name)) entity.Name = request.Name.Trim();
        if (request.Handle is not null) entity.Handle = request.Handle.Trim();
        if (request.Platform is not null) entity.Platform = request.Platform.Trim();
        if (request.Url is not null) entity.Url = request.Url.Trim();
        if (request.Notes is not null) entity.Notes = request.Notes.Trim();
        if (!string.IsNullOrWhiteSpace(request.Confidence)) entity.Confidence = ParseEnum(request.Confidence, entity.Confidence);

        await db.SaveChangesAsync(ct);
        return Results.Ok(DossierEntityDto.From(entity));
    }

    private static async Task<IResult> DeleteDossierEntityAsync(Guid id, VeritasDbContext db, CancellationToken ct)
    {
        var entity = await db.DossierEntities.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null)
        {
            return Results.NotFound();
        }

        db.DossierEntities.Remove(entity);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult> CreateDossierEntityRelationAsync(Guid id, CreateDossierEntityRelationRequest request, VeritasDbContext db, CancellationToken ct)
    {
        var validation = await ValidateEntityRelationAsync(id, request.FromEntityId, request.ToEntityId, db, ct);
        if (validation is not null)
        {
            return validation;
        }

        var relationType = NonBlank(request.RelationType, "related");
        var duplicate = await db.DossierEntityRelations.AnyAsync(x =>
            x.FromEntityId == request.FromEntityId &&
            x.ToEntityId == request.ToEntityId &&
            x.RelationType == relationType, ct);
        if (duplicate)
        {
            return Results.Conflict(new { error = "That entity relation already exists." });
        }

        var relation = new DossierEntityRelation
        {
            DossierId = id,
            FromEntityId = request.FromEntityId,
            ToEntityId = request.ToEntityId,
            RelationType = relationType,
            Confidence = ParseEnum(request.Confidence, ConfidenceLevel.None),
            EvidenceBasis = request.EvidenceBasis?.Trim(),
            Notes = request.Notes?.Trim()
        };

        db.DossierEntityRelations.Add(relation);
        var names = await EntityRelationNamesAsync(relation.FromEntityId, relation.ToEntityId, db, ct);
        AddWorkflowTimeline(
            db,
            id,
            $"Entity relation added: {ShortText(names.From)} -> {ShortText(names.To)}",
            "Entity graph",
            null,
            relation.Confidence,
            $"{relation.RelationType}. {relation.EvidenceBasis ?? relation.Notes}".Trim());
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/dossiers/{id}/entity-relations", DossierEntityRelationDto.From(relation));
    }

    private static async Task<IResult> PatchDossierEntityRelationAsync(Guid id, PatchDossierEntityRelationRequest request, VeritasDbContext db, CancellationToken ct)
    {
        var relation = await db.DossierEntityRelations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (relation is null)
        {
            return Results.NotFound();
        }

        var fromId = request.FromEntityId ?? relation.FromEntityId;
        var toId = request.ToEntityId ?? relation.ToEntityId;
        var validation = await ValidateEntityRelationAsync(relation.DossierId, fromId, toId, db, ct);
        if (validation is not null)
        {
            return validation;
        }

        var relationType = request.RelationType is null ? relation.RelationType : NonBlank(request.RelationType, relation.RelationType);
        var duplicate = await db.DossierEntityRelations.AnyAsync(x =>
            x.Id != relation.Id &&
            x.FromEntityId == fromId &&
            x.ToEntityId == toId &&
            x.RelationType == relationType, ct);
        if (duplicate)
        {
            return Results.Conflict(new { error = "That entity relation already exists." });
        }

        relation.FromEntityId = fromId;
        relation.ToEntityId = toId;
        relation.RelationType = relationType;
        if (!string.IsNullOrWhiteSpace(request.Confidence)) relation.Confidence = ParseEnum(request.Confidence, relation.Confidence);
        if (request.EvidenceBasis is not null) relation.EvidenceBasis = request.EvidenceBasis.Trim();
        if (request.Notes is not null) relation.Notes = request.Notes.Trim();

        var names = await EntityRelationNamesAsync(relation.FromEntityId, relation.ToEntityId, db, ct);
        AddWorkflowTimeline(
            db,
            relation.DossierId,
            $"Entity relation updated: {ShortText(names.From)} -> {ShortText(names.To)}",
            "Entity graph",
            null,
            relation.Confidence,
            $"{relation.RelationType}. {relation.EvidenceBasis ?? relation.Notes}".Trim());
        await db.SaveChangesAsync(ct);
        return Results.Ok(DossierEntityRelationDto.From(relation));
    }

    private static async Task<IResult> DeleteDossierEntityRelationAsync(Guid id, VeritasDbContext db, CancellationToken ct)
    {
        var relation = await db.DossierEntityRelations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (relation is null)
        {
            return Results.NotFound();
        }

        db.DossierEntityRelations.Remove(relation);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<IResult?> ValidateEntityRelationAsync(Guid dossierId, Guid fromEntityId, Guid toEntityId, VeritasDbContext db, CancellationToken ct)
    {
        if (fromEntityId == toEntityId)
        {
            return Results.BadRequest(new { error = "An entity relation must link two different entities." });
        }

        var entityCount = await db.DossierEntities.CountAsync(x => x.DossierId == dossierId && (x.Id == fromEntityId || x.Id == toEntityId), ct);
        return entityCount == 2
            ? null
            : Results.BadRequest(new { error = "Both entities must belong to the same dossier." });
    }

    private static async Task<(string From, string To)> EntityRelationNamesAsync(Guid fromEntityId, Guid toEntityId, VeritasDbContext db, CancellationToken ct)
    {
        var entities = await db.DossierEntities
            .Where(x => x.Id == fromEntityId || x.Id == toEntityId)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct);
        return (
            entities.FirstOrDefault(x => x.Id == fromEntityId)?.Name ?? fromEntityId.ToString("N"),
            entities.FirstOrDefault(x => x.Id == toEntityId)?.Name ?? toEntityId.ToString("N"));
    }

    private static async Task<bool> FindingLinksBelongToDossierAsync(Guid dossierId, Guid? evidenceItemId, Guid? analysisRunId, VeritasDbContext db, CancellationToken ct)
    {
        if (evidenceItemId is not null && !await db.EvidenceItems.AnyAsync(x => x.Id == evidenceItemId.Value && x.DossierId == dossierId, ct))
        {
            return false;
        }

        if (analysisRunId is not null)
        {
            var belongsToDossier = await db.AnalysisRuns
                .Include(x => x.EvidenceItem)
                .AnyAsync(x => x.Id == analysisRunId.Value && x.EvidenceItem != null && x.EvidenceItem.DossierId == dossierId, ct);
            if (!belongsToDossier)
            {
                return false;
            }
        }

        return true;
    }

    private static void AddWorkflowTimeline(
        VeritasDbContext db,
        Guid dossierId,
        string caption,
        string? source,
        string? url,
        ConfidenceLevel confidence,
        string? notes,
        DateTimeOffset? time = null,
        string? platform = null,
        string? evidenceHash = null,
        bool firstKnownAppearance = false)
    {
        db.TimelineEntries.Add(new TimelineEntry
        {
            DossierId = dossierId,
            Time = time ?? DateTimeOffset.UtcNow,
            Platform = platform,
            Url = url,
            Source = source,
            EvidenceHash = evidenceHash,
            Caption = caption,
            Notes = notes,
            Confidence = confidence,
            FirstKnownAppearance = firstKnownAppearance
        });
    }

    private static void AddSuggestedTasks(VeritasDbContext db, Guid dossierId, EvidenceType type, ProvenanceStatus provenance)
    {
        if (type is EvidenceType.Image or EvidenceType.Screenshot)
        {
            if (provenance == ProvenanceStatus.ScreenshotOnly || type == EvidenceType.Screenshot)
            {
                db.InvestigationTasks.Add(new InvestigationTask { DossierId = dossierId, Title = "Provide original source file if this is a screenshot", Description = "Screenshots are useful but weaker than platform originals or camera originals.", Priority = Priority.High, TaskType = InvestigationTaskType.ProvideOriginalMedia });
            }

            db.InvestigationTasks.Add(new InvestigationTask { DossierId = dossierId, Title = "Reverse-search full image", Description = "Search for earlier appearances of the full image.", Priority = Priority.Medium, TaskType = InvestigationTaskType.ReverseImageSearch });
            db.InvestigationTasks.Add(new InvestigationTask { DossierId = dossierId, Title = "Reverse-search cropped background", Description = "Crop background regions and compare against earlier known images.", Priority = Priority.Medium, TaskType = InvestigationTaskType.ReverseImageSearch });
            db.InvestigationTasks.Add(new InvestigationTask { DossierId = dossierId, Title = "Reverse-search clothing, objects, and background", Description = "Object-specific crops can reveal stolen or composite source material.", Priority = Priority.Low, TaskType = InvestigationTaskType.ReverseImageSearch });
            db.InvestigationTasks.Add(new InvestigationTask { DossierId = dossierId, Title = "Compare against earlier known images", Description = "Use source chronology to avoid overclaiming from compression artifacts.", Priority = Priority.Medium, TaskType = InvestigationTaskType.SourceChronology });
        }

        if (type == EvidenceType.Video)
        {
            db.InvestigationTasks.Add(new InvestigationTask { DossierId = dossierId, Title = "Inspect corner crops", Description = "Review corner crops for watermark remnants or platform UI traces.", Priority = Priority.Medium, TaskType = InvestigationTaskType.ExtractVideoFrames });
            db.InvestigationTasks.Add(new InvestigationTask { DossierId = dossierId, Title = "OCR watermark remnants", Description = "Use OCR externally if Tesseract is unavailable locally.", Priority = Priority.Low, TaskType = InvestigationTaskType.ManualVerification });
            db.InvestigationTasks.Add(new InvestigationTask { DossierId = dossierId, Title = "Compare first, middle, and last frames", Description = "Look for temporal inconsistencies without treating one heuristic as proof of AI.", Priority = Priority.Medium, TaskType = InvestigationTaskType.ExtractVideoFrames });
            db.InvestigationTasks.Add(new InvestigationTask { DossierId = dossierId, Title = "Search audio and source", Description = "Locate earlier or platform-original versions if known.", Priority = Priority.Medium, TaskType = InvestigationTaskType.SourceChronology });
            db.InvestigationTasks.Add(new InvestigationTask { DossierId = dossierId, Title = "Upload source TikTok if known", Description = "Prefer original platform context over reposted or screen-recorded copies.", Priority = Priority.Low, TaskType = InvestigationTaskType.ProvideOriginalMedia });
        }
    }

    private static EvidenceType GuessEvidenceType(string? contentType, string filename, string? requested)
    {
        if (!string.IsNullOrWhiteSpace(requested) && Enum.TryParse<EvidenceType>(requested, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        if (contentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
        {
            return EvidenceType.Image;
        }

        if (contentType?.StartsWith("video/", StringComparison.OrdinalIgnoreCase) == true)
        {
            return EvidenceType.Video;
        }

        return Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" => EvidenceType.Image,
            ".mp4" or ".mov" or ".mkv" or ".webm" => EvidenceType.Video,
            ".txt" or ".md" => EvidenceType.Text,
            _ => EvidenceType.Other
        };
    }

    private static string GuessPlatform(Uri uri)
    {
        var host = uri.Host.ToLowerInvariant();
        if (host is "x.com" or "twitter.com" or "www.x.com" or "www.twitter.com" || host.EndsWith(".twitter.com", StringComparison.Ordinal))
        {
            return "X";
        }

        if (host.Contains("tiktok", StringComparison.Ordinal)) return "TikTok";
        if (host.Contains("youtube", StringComparison.Ordinal) || host.Contains("youtu.be", StringComparison.Ordinal)) return "YouTube";
        if (host.Contains("instagram", StringComparison.Ordinal)) return "Instagram";
        if (host.Contains("bsky", StringComparison.Ordinal)) return "Bluesky";
        if (host.Contains("mastodon", StringComparison.Ordinal)) return "Mastodon";
        return host;
    }

    private static bool IsTermsSensitivePlatform(string platform)
    {
        return platform is "X" or "TikTok" or "YouTube" or "Instagram";
    }

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
    {
        return !string.IsNullOrWhiteSpace(value) && Enum.TryParse<TEnum>(value.Replace("'", "").Replace("-", ""), ignoreCase: true, out var parsed)
            ? parsed
            : fallback;
    }

    private static Guid? TryParseGuid(string? value)
    {
        return Guid.TryParse(value, out var parsed) ? parsed : null;
    }

    private static string SanitizeTitle(string title)
    {
        var safe = string.IsNullOrWhiteSpace(title) ? "text-sample" : title.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            safe = safe.Replace(invalid, '-');
        }

        return safe.Length > 80 ? safe[..80] : safe;
    }

    private static string UniqueZipName(string filename, ISet<string> usedNames)
    {
        var safe = string.IsNullOrWhiteSpace(filename) ? "artifact.bin" : Path.GetFileName(filename);
        if (usedNames.Add(safe))
        {
            return safe;
        }

        var stem = Path.GetFileNameWithoutExtension(safe);
        var extension = Path.GetExtension(safe);
        for (var i = 2; i < 1000; i++)
        {
            var candidate = $"{stem}-{i}{extension}";
            if (usedNames.Add(candidate))
            {
                return candidate;
            }
        }

        return $"{Guid.NewGuid():N}{extension}";
    }

    private static string ShortText(string text, int maxLength = 96)
    {
        var normalized = string.Join(' ', text.Split(['\r', '\n', '\t', ' '], StringSplitOptions.RemoveEmptyEntries));
        return normalized.Length <= maxLength ? normalized : $"{normalized[..maxLength]}...";
    }

    private static List<string> DetectTextSignals(string text)
    {
        var signals = new List<string>();
        var lower = text.ToLowerInvariant();
        var phraseSignals = new Dictionary<string, string>
        {
            ["as an ai"] = "Contains explicit AI-assistant framing.",
            ["i cannot"] = "Contains refusal-style assistant language.",
            ["delve"] = "Uses common low-specificity AI-writing vocabulary.",
            ["tapestry"] = "Uses common low-specificity AI-writing vocabulary.",
            ["seamless"] = "Uses common low-specificity AI-writing vocabulary.",
            ["robust"] = "Uses common low-specificity AI-writing vocabulary.",
            ["underscore"] = "Uses common low-specificity AI-writing vocabulary.",
            ["it is important to note"] = "Uses generic caveat framing.",
            ["in conclusion"] = "Uses formulaic concluding structure."
        };

        foreach (var (phrase, description) in phraseSignals)
        {
            if (lower.Contains(phrase, StringComparison.Ordinal))
            {
                signals.Add(description);
            }
        }

        var sentences = text.Split(['.', '!', '?'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (sentences.Length >= 5)
        {
            var lengths = sentences.Select(x => x.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length).ToArray();
            var average = lengths.Average();
            var variance = lengths.Select(x => Math.Pow(x - average, 2)).Average();
            if (average > 16 && variance < 18)
            {
                signals.Add("Sentence lengths are unusually uniform for a longer sample.");
            }
        }

        var words = lower.Split([' ', '\r', '\n', '\t', ',', '.', ';', ':', '!', '?', '"', '\'', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 120)
        {
            var uniqueRatio = words.Distinct().Count() / (double)words.Length;
            if (uniqueRatio < 0.42)
            {
                signals.Add("Vocabulary repetition is high for the sample length.");
            }
        }

        if (text.Count(x => x == '-') >= 4 && text.Length < 2000)
        {
            signals.Add("Dash-heavy phrasing may indicate templated or heavily edited prose.");
        }

        return signals.Distinct().Take(8).ToList();
    }

    private static string NonBlank(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
