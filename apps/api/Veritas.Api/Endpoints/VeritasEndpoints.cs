using System.Security.Cryptography;
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
            var projects = await db.Projects.Include(x => x.Dossiers).OrderByDescending(x => x.UpdatedAt).ToListAsync(ct);
            return Results.Ok(projects.Select(ProjectDto.From));
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
            var dossiers = await db.Dossiers.Where(x => x.ProjectId == projectId).OrderByDescending(x => x.UpdatedAt).ToListAsync(ct);
            return Results.Ok(dossiers.Select(DossierDto.From));
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
                    timeline = dossier.TimelineEntries.OrderBy(x => x.Time).Select(TimelineEntryDto.From)
                });
        });

        api.MapPost("/dossiers/{id:guid}/sources/url", AddUrlSourceAsync);
        api.MapGet("/dossiers/{id:guid}/sources", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var sources = await db.Sources.Where(x => x.DossierId == id).OrderByDescending(x => x.ObservedAt).ToListAsync(ct);
            return Results.Ok(sources.Select(SourceDto.From));
        });

        api.MapPost("/dossiers/{id:guid}/evidence/upload", UploadEvidenceAsync)
            .DisableAntiforgery();
        api.MapGet("/dossiers/{id:guid}/evidence", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var evidence = await db.EvidenceItems
                .Include(x => x.AnalysisRuns).ThenInclude(x => x.Artifacts)
                .Where(x => x.DossierId == id)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync(ct);
            return Results.Ok(evidence.Select(EvidenceDto.From));
        });
        api.MapGet("/evidence/{id:guid}", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var item = await db.EvidenceItems
                .Include(x => x.AnalysisRuns).ThenInclude(x => x.Artifacts)
                .FirstOrDefaultAsync(x => x.Id == id, ct);
            return item is null ? Results.NotFound() : Results.Ok(EvidenceDto.From(item));
        });
        api.MapGet("/evidence/{id:guid}/file", DownloadEvidenceAsync);

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
            var runs = await db.AnalysisRuns.Include(x => x.Artifacts).Where(x => x.EvidenceItemId == id).OrderByDescending(x => x.StartedAt).ToListAsync(ct);
            return Results.Ok(runs.Select(AnalysisRunDto.From));
        });
        api.MapGet("/analysis-artifacts/{id:guid}", DownloadArtifactAsync);

        api.MapGet("/dossiers/{id:guid}/findings", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var findings = await db.Findings.Where(x => x.DossierId == id).OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
            return Results.Ok(findings.Select(FindingDto.From));
        });
        api.MapPost("/dossiers/{id:guid}/findings", CreateFindingAsync);

        api.MapGet("/dossiers/{id:guid}/claims", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var claims = await db.Claims.Where(x => x.DossierId == id).OrderBy(x => x.CreatedAt).ToListAsync(ct);
            return Results.Ok(claims.Select(ClaimDto.From));
        });
        api.MapPost("/dossiers/{id:guid}/claims", CreateClaimAsync);
        api.MapPatch("/claims/{id:guid}", PatchClaimAsync);

        api.MapGet("/dossiers/{id:guid}/tasks", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var tasks = await db.InvestigationTasks.Where(x => x.DossierId == id).OrderByDescending(x => x.Priority).ThenBy(x => x.CreatedAt).ToListAsync(ct);
            return Results.Ok(tasks.Select(InvestigationTaskDto.From));
        });
        api.MapPost("/dossiers/{id:guid}/tasks", CreateTaskAsync);
        api.MapPatch("/tasks/{id:guid}", PatchTaskAsync);

        api.MapGet("/dossiers/{id:guid}/timeline", async (Guid id, VeritasDbContext db, CancellationToken ct) =>
        {
            var entries = await db.TimelineEntries.Where(x => x.DossierId == id).OrderBy(x => x.Time).ToListAsync(ct);
            return Results.Ok(entries.Select(TimelineEntryDto.From));
        });
        api.MapPost("/dossiers/{id:guid}/timeline", CreateTimelineEntryAsync);

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

        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/dossiers/{id}/sources", SourceDto.From(source));
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
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/evidence/{evidence.Id}", EvidenceDto.From(evidence));
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
        await db.SaveChangesAsync(ct);
        return Results.Created($"/api/dossiers/{id}/findings", FindingDto.From(finding));
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

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            claim.Status = ParseEnum(request.Status, claim.Status);
        }

        if (!string.IsNullOrWhiteSpace(request.Confidence))
        {
            claim.Confidence = ParseEnum(request.Confidence, claim.Confidence);
        }

        if (request.Rationale is not null)
        {
            claim.Rationale = request.Rationale;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(ClaimDto.From(claim));
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

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            task.Status = ParseEnum(request.Status, task.Status);
            task.CompletedAt = task.Status == InvestigationTaskStatus.Done ? DateTimeOffset.UtcNow : null;
        }

        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            task.Priority = ParseEnum(request.Priority, task.Priority);
        }

        if (request.Description is not null)
        {
            task.Description = request.Description;
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(InvestigationTaskDto.From(task));
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

    private static string NonBlank(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }
}
