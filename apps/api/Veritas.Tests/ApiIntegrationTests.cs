using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Veritas.Application.Policies;
using Veritas.Domain;
using Veritas.Domain.Entities;
using Veritas.Infrastructure.Analysis;
using Veritas.Infrastructure.Persistence;

namespace Veritas.Tests;

public sealed class ApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Project_and_dossier_creation_work()
    {
        using var factory = new TestApiFactory();
        var client = await factory.CreateReadyClientAsync();

        var projectResponse = await client.PostAsJsonAsync("/api/projects", new { name = "Investigation", description = "Test project" });
        Assert.Equal(HttpStatusCode.Created, projectResponse.StatusCode);
        var project = await ReadAsync<ProjectDto>(projectResponse);

        var dossierResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/dossiers", new { title = "Source dossier", summary = "Check provenance" });
        Assert.Equal(HttpStatusCode.Created, dossierResponse.StatusCode);
        var dossier = await ReadAsync<DossierDto>(dossierResponse);

        var loaded = await client.GetFromJsonAsync<ProjectDetails>($"/api/projects/{project.Id}", JsonOptions);
        Assert.NotNull(loaded);
        Assert.Equal("Investigation", loaded!.Project.Name);
        Assert.Contains(loaded.Dossiers, x => x.Id == dossier.Id);
    }

    [Fact]
    public async Task X_url_flow_records_policy_and_requests_manual_original_media()
    {
        using var factory = new TestApiFactory(robotsAllowed: true);
        var client = await factory.CreateReadyClientAsync();
        var dossier = await CreateDossierAsync(client);

        var response = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sources/url", new { url = "https://x.com/example/status/123", title = "Post" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var source = await ReadAsync<SourceDto>(response);
        Assert.Equal("X", source.Platform);
        Assert.Equal("RequiresManualInput", source.CollectionStatus);
        Assert.Contains("manual", source.Notes, StringComparison.OrdinalIgnoreCase);

        var tasks = await client.GetFromJsonAsync<List<InvestigationTaskDto>>($"/api/dossiers/{dossier.Id}/tasks", JsonOptions);
        Assert.Contains(tasks!, x => x.Title.Contains("pbs.twimg.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Blocked_robots_decision_is_visible()
    {
        using var factory = new TestApiFactory(robotsAllowed: false);
        var client = await factory.CreateReadyClientAsync();
        var dossier = await CreateDossierAsync(client);

        var response = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sources/url", new { url = "https://example.test/private/media.jpg" });

        var source = await ReadAsync<SourceDto>(response);
        Assert.Equal("BlockedByRobots", source.CollectionStatus);
        Assert.Contains("disallows", source.RobotsDecision, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Evidence_upload_hashes_and_creates_image_tasks()
    {
        using var factory = new TestApiFactory();
        var client = await factory.CreateReadyClientAsync();
        var dossier = await CreateDossierAsync(client);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Screenshot upload"), "title");
        content.Add(new StringContent("ScreenshotOnly"), "provenanceStatus");
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("not a real image but enough for upload hashing")), "file", "sample.jpg");

        var response = await client.PostAsync($"/api/dossiers/{dossier.Id}/evidence/upload", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var evidence = await ReadAsync<EvidenceDto>(response);
        Assert.Equal("Image", evidence.Type);
        Assert.False(string.IsNullOrWhiteSpace(evidence.ContentHashSha256));

        var tasks = await client.GetFromJsonAsync<List<InvestigationTaskDto>>($"/api/dossiers/{dossier.Id}/tasks", JsonOptions);
        Assert.Contains(tasks!, x => x.TaskType == "ReverseImageSearch");
        Assert.Contains(tasks!, x => x.TaskType == "ProvideOriginalMedia");
    }

    [Fact]
    public async Task Text_triage_creates_completed_analysis_run()
    {
        using var factory = new TestApiFactory();
        var client = await factory.CreateReadyClientAsync();
        var dossier = await CreateDossierAsync(client);

        var response = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/text/triage", new
        {
            title = "Text sample",
            text = "This is a lightweight text triage sample for cautious review."
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await ReadAsync<TextTriageResultDto>(response);
        Assert.Equal("Text", result.Evidence.Type);
        var run = Assert.Single(result.Evidence.AnalysisRuns);
        Assert.Equal("text-triage", run.Pipeline);
        Assert.Equal("Completed", run.Status);
        Assert.NotNull(run.StartedAt);
        Assert.NotNull(run.CompletedAt);
        Assert.Contains("signalText", run.ResultJson);
        Assert.Equal(run.Id, result.Finding.AnalysisRunId);
    }

    [Fact]
    public async Task Text_evidence_analysis_can_be_rerun()
    {
        using var factory = new TestApiFactory();
        var client = await factory.CreateReadyClientAsync();
        var dossier = await CreateDossierAsync(client);
        var triageResponse = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/text/triage", new
        {
            title = "Retry text sample",
            text = "This is a retryable text triage sample with robust generic phrasing."
        });
        var triage = await ReadAsync<TextTriageResultDto>(triageResponse);

        var rerunResponse = await client.PostAsync($"/api/evidence/{triage.Evidence.Id}/analysis/text", null);

        Assert.Equal(HttpStatusCode.Created, rerunResponse.StatusCode);
        var run = await ReadAsync<AnalysisRunDto>(rerunResponse);
        Assert.Equal("text-triage", run.Pipeline);
        Assert.Equal("Completed", run.Status);
        Assert.Contains("signalText", run.ResultJson);
    }

    [Fact]
    public async Task Analysis_run_status_transitions_are_persisted()
    {
        using var factory = new TestApiFactory();
        var client = await factory.CreateReadyClientAsync();
        var dossier = await CreateDossierAsync(client);
        var evidence = await UploadTinyEvidenceAsync(client, dossier.Id);

        var response = await client.PostAsync($"/api/evidence/{evidence.Id}/analysis/image", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var run = await ReadAsync<AnalysisRunDto>(response);
        Assert.Equal("Pending", run.Status);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VeritasDbContext>();
        var entity = await db.AnalysisRuns.FirstAsync(x => x.Id == run.Id);
        entity.Status = AnalysisStatus.Running;
        await db.SaveChangesAsync();
        entity.Status = AnalysisStatus.Completed;
        entity.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var runs = await client.GetFromJsonAsync<List<AnalysisRunDto>>($"/api/evidence/{evidence.Id}/analysis-runs", JsonOptions);
        Assert.Contains(runs!, x => x.Id == run.Id && x.Status == "Completed");
    }

    [Fact]
    public async Task Running_analysis_without_start_time_is_closed_as_failed()
    {
        using var factory = new TestApiFactory();
        var client = await factory.CreateReadyClientAsync();
        var dossier = await CreateDossierAsync(client);
        var evidence = await UploadTinyEvidenceAsync(client, dossier.Id);

        var response = await client.PostAsync($"/api/evidence/{evidence.Id}/analysis/image", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var run = await ReadAsync<AnalysisRunDto>(response);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VeritasDbContext>();
        var entity = await db.AnalysisRuns.FirstAsync(x => x.Id == run.Id);
        entity.Status = AnalysisStatus.Running;
        entity.StartedAt = null;
        await db.SaveChangesAsync();

        var processor = scope.ServiceProvider.GetRequiredService<AnalysisRunProcessor>();
        await processor.ProcessAsync(run.Id, CancellationToken.None);

        db.ChangeTracker.Clear();
        var updated = await db.AnalysisRuns.FirstAsync(x => x.Id == run.Id);
        Assert.Equal(AnalysisStatus.Failed, updated.Status);
        Assert.NotNull(updated.CompletedAt);
        Assert.Contains("without a start time", updated.Error);
    }

    [Fact]
    public async Task Manual_workflow_changes_create_timeline_entries()
    {
        using var factory = new TestApiFactory();
        var client = await factory.CreateReadyClientAsync();
        var dossier = await CreateDossierAsync(client);

        var findingResponse = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/findings", new
        {
            category = "ManualObservation",
            claim = "A manual source comparison is needed.",
            confidence = "Low",
            direction = "Inconclusive",
            evidence = "Initial analyst note.",
            limitations = "Needs corroboration.",
            falsificationPath = "Find earlier source copies."
        });
        var finding = await ReadAsync<FindingDto>(findingResponse);
        await client.PatchAsJsonAsync($"/api/findings/{finding.Id}", new { confidence = "Medium", direction = "Neutral" });

        var claimResponse = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/claims", new { text = "The image was reposted.", status = "Unassessed", confidence = "Low" });
        var claim = await ReadAsync<ClaimDto>(claimResponse);
        await client.PatchAsJsonAsync($"/api/claims/{claim.Id}", new { status = "Plausible", confidence = "Medium" });

        var taskResponse = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/tasks", new { title = "Check archived copies", priority = "High", taskType = "SourceChronology" });
        var task = await ReadAsync<InvestigationTaskDto>(taskResponse);
        await client.PatchAsJsonAsync($"/api/tasks/{task.Id}", new { status = "Done" });

        var timeline = await client.GetFromJsonAsync<List<TimelineEntryDto>>($"/api/dossiers/{dossier.Id}/timeline", JsonOptions);
        Assert.NotNull(timeline);
        Assert.Contains(timeline!, x => x.Caption.Contains("Finding recorded", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(timeline!, x => x.Caption.Contains("Finding updated", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(timeline!, x => x.Caption.Contains("Claim opened", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(timeline!, x => x.Caption.Contains("Claim updated", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(timeline!, x => x.Caption.Contains("Task opened", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(timeline!, x => x.Caption.Contains("Task Done", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Claims_can_link_evidence_with_stance()
    {
        using var factory = new TestApiFactory();
        var client = await factory.CreateReadyClientAsync();
        var dossier = await CreateDossierAsync(client);
        var evidence = await UploadTinyEvidenceAsync(client, dossier.Id);

        var claimResponse = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/claims", new
        {
            text = "The media was reposted.",
            status = "Weak",
            confidence = "Low",
            evidenceItemId = evidence.Id,
            evidenceStance = "Supports",
            evidenceNote = "Uploaded evidence shows the relevant media."
        });

        var claim = await ReadAsync<ClaimDto>(claimResponse);
        var link = Assert.Single(claim.EvidenceLinks);
        Assert.Equal(evidence.Id, link.EvidenceItemId);
        Assert.Equal("Supports", link.Stance);

        var patchResponse = await client.PatchAsJsonAsync($"/api/claim-evidence-links/{link.Id}", new { stance = "Mixed", note = "Partially supports the claim." });
        var patched = await ReadAsync<ClaimEvidenceLinkDto>(patchResponse);
        Assert.Equal("Mixed", patched.Stance);

        var deleteResponse = await client.DeleteAsync($"/api/claim-evidence-links/{link.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
    }

    [Fact]
    public async Task Entity_relations_can_be_created_updated_listed_and_deleted()
    {
        using var factory = new TestApiFactory();
        var client = await factory.CreateReadyClientAsync();
        var dossier = await CreateDossierAsync(client);

        var firstResponse = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/entities", new { kind = "SocialAccount", name = "Account A", handle = "@a", confidence = "Medium" });
        var secondResponse = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/entities", new { kind = "Organization", name = "Org B", confidence = "Low" });
        var first = await ReadAsync<DossierEntityDto>(firstResponse);
        var second = await ReadAsync<DossierEntityDto>(secondResponse);

        var selfLink = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/entity-relations", new
        {
            fromEntityId = first.Id,
            toEntityId = first.Id,
            relationType = "alias of"
        });
        Assert.Equal(HttpStatusCode.BadRequest, selfLink.StatusCode);

        var relationResponse = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/entity-relations", new
        {
            fromEntityId = first.Id,
            toEntityId = second.Id,
            relationType = "operates",
            confidence = "Low",
            evidenceBasis = "Profile bio links to the organization."
        });
        var relation = await ReadAsync<DossierEntityRelationDto>(relationResponse);
        Assert.Equal("operates", relation.RelationType);

        var patchedResponse = await client.PatchAsJsonAsync($"/api/entity-relations/{relation.Id}", new
        {
            relationType = "member of",
            confidence = "Medium",
            notes = "Patched relation note."
        });
        var patched = await ReadAsync<DossierEntityRelationDto>(patchedResponse);
        Assert.Equal("member of", patched.RelationType);
        Assert.Equal("Medium", patched.Confidence);

        var listed = await client.GetFromJsonAsync<List<DossierEntityRelationDto>>($"/api/dossiers/{dossier.Id}/entity-relations", JsonOptions);
        Assert.Contains(listed!, x => x.Id == relation.Id);

        var bundle = await client.GetFromJsonAsync<DossierBundleDto>($"/api/dossiers/{dossier.Id}", JsonOptions);
        Assert.Contains(bundle!.EntityRelations, x => x.Id == relation.Id);

        var delete = await client.DeleteAsync($"/api/entity-relations/{relation.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        listed = await client.GetFromJsonAsync<List<DossierEntityRelationDto>>($"/api/dossiers/{dossier.Id}/entity-relations", JsonOptions);
        Assert.DoesNotContain(listed!, x => x.Id == relation.Id);
    }

    [Fact]
    public async Task Sources_can_link_author_entity()
    {
        using var factory = new TestApiFactory(robotsAllowed: true);
        var client = await factory.CreateReadyClientAsync();
        var dossier = await CreateDossierAsync(client);
        var entityResponse = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/entities", new { kind = "SocialAccount", name = "Account A", handle = "@a", confidence = "Medium" });
        var entity = await ReadAsync<DossierEntityDto>(entityResponse);

        var sourceResponse = await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/sources/url", new
        {
            url = "https://example.test/post/1",
            title = "Entity authored post",
            authorEntityId = entity.Id
        });

        var source = await ReadAsync<SourceDto>(sourceResponse);
        Assert.Equal(entity.Id, source.AuthorEntityId);

        var patchResponse = await client.PatchAsJsonAsync($"/api/sources/{source.Id}", new { clearAuthorEntity = true, authorHandle = "@fallback" });
        var patched = await ReadAsync<SourceDto>(patchResponse);
        Assert.Null(patched.AuthorEntityId);
        Assert.Equal("@fallback", patched.AuthorHandle);
    }

    [Fact]
    public async Task Report_markdown_uses_cautious_language()
    {
        using var factory = new TestApiFactory();
        var client = await factory.CreateReadyClientAsync();
        var dossier = await CreateDossierAsync(client);
        await client.PostAsJsonAsync($"/api/dossiers/{dossier.Id}/claims", new { text = "The media appears AI-generated", status = "Weak", confidence = "Low" });

        var markdown = await client.GetStringAsync($"/api/dossiers/{dossier.Id}/report/markdown");

        Assert.Contains("not an AI detector", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not establish", markdown, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<DossierDto> CreateDossierAsync(HttpClient client)
    {
        var projectResponse = await client.PostAsJsonAsync("/api/projects", new { name = "Project" });
        var project = await ReadAsync<ProjectDto>(projectResponse);
        var dossierResponse = await client.PostAsJsonAsync($"/api/projects/{project.Id}/dossiers", new { title = "Dossier" });
        return await ReadAsync<DossierDto>(dossierResponse);
    }

    private static async Task<EvidenceDto> UploadTinyEvidenceAsync(HttpClient client, Guid dossierId)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Sample"), "title");
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes("sample")), "file", "sample.jpg");
        var response = await client.PostAsync($"/api/dossiers/{dossierId}/evidence/upload", content);
        return await ReadAsync<EvidenceDto>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        Assert.NotNull(value);
        return value!;
    }

    private sealed class TestApiFactory(bool robotsAllowed = true) : WebApplicationFactory<Program>
    {
        private readonly string _dbPath = Path.Combine(Path.GetTempPath(), "veritas-tests", $"{Guid.NewGuid():N}.db");
        private readonly string _storageRoot = Path.Combine(Path.GetTempPath(), "veritas-tests", Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration(config =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Analysis:RunBackgroundWorker"] = "false",
                    ["Database:Provider"] = "Sqlite",
                    ["Database:ApplyMigrations"] = "false",
                    ["Demo:SeedData"] = "false",
                    ["Storage:RootPath"] = _storageRoot,
                    ["ConnectionStrings:Sqlite"] = $"Data Source={_dbPath}"
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRobotsPolicyService>();
                services.RemoveAll<IHostedService>();
                services.AddSingleton<IRobotsPolicyService>(new FakeRobotsPolicyService(robotsAllowed));
            });
        }

        public async Task<HttpClient> CreateReadyClientAsync()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            Directory.CreateDirectory(_storageRoot);
            var client = CreateClient();
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VeritasDbContext>();
            await db.Database.EnsureCreatedAsync();
            return client;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, recursive: true);
            }

            foreach (var path in new[] { _dbPath, $"{_dbPath}-wal", $"{_dbPath}-shm" })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    private sealed class FakeRobotsPolicyService(bool allowed) : IRobotsPolicyService
    {
        public Task<RobotsDecision> CanFetchAsync(Uri uri, string userAgent, CancellationToken ct)
        {
            return Task.FromResult(new RobotsDecision(
                uri.ToString(),
                uri.Host,
                allowed,
                allowed ? "robots.txt allows this path for tests." : "robots.txt disallows this path; manual collection is required.",
                allowed ? "Allow: /" : "Disallow: /private",
                DateTimeOffset.UtcNow,
                $"{uri.Scheme}://{uri.Host}/robots.txt",
                "test-hash"));
        }
    }

    private sealed record ProjectDetails(ProjectDto Project, List<DossierDto> Dossiers);
    private sealed record ProjectDto(Guid Id, string Name, string? Description, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int DossierCount);
    private sealed record DossierDto(Guid Id, Guid ProjectId, string Title, string? Summary, string Status, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
    private sealed record SourceDto(Guid Id, Guid DossierId, string Type, string? Url, string? Platform, string? Title, string? AuthorHandle, Guid? AuthorEntityId, DateTimeOffset? ObservedAt, DateTimeOffset? FirstSeenAt, string CollectionStatus, string? RobotsDecision, string? Notes);
    private sealed record EvidenceDto(Guid Id, Guid DossierId, Guid? SourceId, string Type, string Title, string? Description, string? OriginalFilename, string? ContentHashSha256, string? PerceptualHash, string? MimeType, long? FileSizeBytes, int? Width, int? Height, double? DurationSeconds, DateTimeOffset? CapturedAt, DateTimeOffset UploadedAt, string ProvenanceStatus, string FileUrl, List<AnalysisRunDto> AnalysisRuns);
    private sealed record AnalysisRunDto(Guid Id, Guid EvidenceItemId, string Pipeline, string Status, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? ToolVersion, string? Summary, string? Error, string? ResultJson, List<object> Artifacts);
    private sealed record TextTriageResultDto(EvidenceDto Evidence, FindingDto Finding, List<InvestigationTaskDto> Tasks, TimelineEntryDto TimelineEntry, List<string> Signals);
    private sealed record FindingDto(Guid Id, Guid DossierId, Guid? EvidenceItemId, Guid? AnalysisRunId, string Category, string Claim, string Confidence, string Direction, string Evidence, string Limitations, string FalsificationPath, DateTimeOffset CreatedAt);
    private sealed record ClaimDto(Guid Id, Guid DossierId, string Text, string Status, string Confidence, string? Rationale, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, List<ClaimEvidenceLinkDto> EvidenceLinks);
    private sealed record ClaimEvidenceLinkDto(Guid Id, Guid ClaimId, Guid EvidenceItemId, string Stance, string? Note, DateTimeOffset CreatedAt);
    private sealed record InvestigationTaskDto(Guid Id, Guid DossierId, string Title, string? Description, string Status, string Priority, string TaskType, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);
    private sealed record TimelineEntryDto(Guid Id, Guid DossierId, DateTimeOffset Time, string? Platform, string? Url, string? Source, string? EvidenceHash, string? Caption, bool FirstKnownAppearance, string? Notes, string Confidence);
    private sealed record DossierEntityDto(Guid Id, Guid DossierId, string Kind, string Name, string? Handle, string? Platform, string? Url, string? Notes, string Confidence, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
    private sealed record DossierEntityRelationDto(Guid Id, Guid DossierId, Guid FromEntityId, Guid ToEntityId, string RelationType, string Confidence, string? EvidenceBasis, string? Notes, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
    private sealed record DossierBundleDto(DossierDto Dossier, List<SourceDto> Sources, List<EvidenceDto> Evidence, List<FindingDto> Findings, List<ClaimDto> Claims, List<InvestigationTaskDto> Tasks, List<TimelineEntryDto> Timeline, List<DossierEntityDto> Entities, List<DossierEntityRelationDto> EntityRelations);
}
