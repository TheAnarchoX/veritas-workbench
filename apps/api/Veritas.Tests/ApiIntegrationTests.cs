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
    private sealed record SourceDto(Guid Id, Guid DossierId, string Type, string? Url, string? Platform, string? Title, string? AuthorHandle, DateTimeOffset? ObservedAt, DateTimeOffset? FirstSeenAt, string CollectionStatus, string? RobotsDecision, string? Notes);
    private sealed record EvidenceDto(Guid Id, Guid DossierId, Guid? SourceId, string Type, string Title, string? Description, string? OriginalFilename, string? ContentHashSha256, string? PerceptualHash, string? MimeType, long? FileSizeBytes, int? Width, int? Height, double? DurationSeconds, DateTimeOffset? CapturedAt, DateTimeOffset UploadedAt, string ProvenanceStatus, string FileUrl, List<AnalysisRunDto> AnalysisRuns);
    private sealed record AnalysisRunDto(Guid Id, Guid EvidenceItemId, string Pipeline, string Status, DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, string? ToolVersion, string? Summary, string? Error, List<object> Artifacts);
    private sealed record InvestigationTaskDto(Guid Id, Guid DossierId, string Title, string? Description, string Status, string Priority, string TaskType, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt);
}
