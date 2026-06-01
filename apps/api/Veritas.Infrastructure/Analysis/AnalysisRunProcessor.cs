using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Veritas.Application.Storage;
using Veritas.Domain;
using Veritas.Domain.Entities;
using Veritas.Infrastructure.Persistence;

namespace Veritas.Infrastructure.Analysis;

public sealed class AnalysisRunProcessor(
    VeritasDbContext db,
    IArtifactStorage artifactStorage,
    IOptions<ForensicsOptions> options,
    ILogger<AnalysisRunProcessor> logger)
{
    private readonly ForensicsOptions _options = options.Value;

    public async Task ProcessAsync(Guid analysisRunId, CancellationToken ct)
    {
        var run = await db.AnalysisRuns
            .Include(x => x.EvidenceItem)
            .FirstOrDefaultAsync(x => x.Id == analysisRunId, ct);

        if (run?.EvidenceItem is null)
        {
            return;
        }

        var artifactDirectory = artifactStorage.GetArtifactDirectory(run.Id);
        var resultPath = Path.Combine(artifactDirectory, "result.json");
        var inputPath = artifactStorage.GetAbsolutePath(run.EvidenceItem.StoragePath);

        try
        {
            var timeout = TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds));

            if (run.Status == AnalysisStatus.Running)
            {
                if (File.Exists(resultPath))
                {
                    await CompleteFromResultAsync(run, artifactDirectory, resultPath, ct);
                    return;
                }

                if (run.StartedAt is null)
                {
                    run.Status = AnalysisStatus.Failed;
                    run.CompletedAt = DateTimeOffset.UtcNow;
                    run.Error = "Analysis run was marked running without a start time.";
                    run.Summary = "Analysis state was inconsistent and has been closed.";
                    await db.SaveChangesAsync(ct);
                    return;
                }

                if (DateTimeOffset.UtcNow - run.StartedAt >= timeout)
                {
                    run.Status = AnalysisStatus.Failed;
                    run.CompletedAt = DateTimeOffset.UtcNow;
                    run.Error = $"Forensic worker exceeded timeout of {_options.TimeoutSeconds} seconds without writing a result.";
                    run.Summary = "Analysis failed before a result file was written.";
                    await db.SaveChangesAsync(ct);
                }

                return;
            }

            if (run.Status != AnalysisStatus.Pending)
            {
                return;
            }

            run.Status = AnalysisStatus.Running;
            run.StartedAt = DateTimeOffset.UtcNow;
            run.Summary = "Forensic worker is running.";
            await db.SaveChangesAsync(ct);

            var workerDirectory = ResolveWorkerDirectory();
            var psi = new ProcessStartInfo
            {
                FileName = _options.PythonExecutable,
                WorkingDirectory = workerDirectory,
                RedirectStandardError = false,
                RedirectStandardOutput = false,
                UseShellExecute = false
            };
            psi.ArgumentList.Add("-m");
            psi.ArgumentList.Add("veritas_forensics");
            psi.ArgumentList.Add(run.Pipeline == "video" ? "analyze-video" : "analyze-image");
            psi.ArgumentList.Add("--input");
            psi.ArgumentList.Add(inputPath);
            psi.ArgumentList.Add("--out");
            psi.ArgumentList.Add(artifactDirectory);
            psi.ArgumentList.Add("--json");
            psi.ArgumentList.Add(resultPath);

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start forensic worker.");
            var deadline = DateTimeOffset.UtcNow.Add(timeout);
            var exitTask = process.WaitForExitAsync(ct);
            logger.LogInformation("Analysis run {AnalysisRunId} started forensic worker in {WorkerDirectory}.", run.Id, workerDirectory);

            while (!File.Exists(resultPath) && DateTimeOffset.UtcNow < deadline)
            {
                var completedTask = await Task.WhenAny(exitTask, Task.Delay(TimeSpan.FromMilliseconds(250), ct));
                if (completedTask == exitTask && !File.Exists(resultPath))
                {
                    throw new InvalidOperationException($"Forensic worker exited with code {process.ExitCode} before writing result.json.");
                }
            }

            if (!File.Exists(resultPath))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException)
                {
                    // The process may have exited without producing the expected result file.
                }

                throw new TimeoutException($"Forensic worker exceeded timeout of {_options.TimeoutSeconds} seconds.");
            }

            await CompleteFromResultAsync(run, artifactDirectory, resultPath, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Analysis run {AnalysisRunId} failed.", run.Id);
            db.ChangeTracker.Clear();

            var failedRun = await db.AnalysisRuns.FirstOrDefaultAsync(x => x.Id == analysisRunId, ct);
            if (failedRun is null)
            {
                return;
            }

            failedRun.Status = AnalysisStatus.Failed;
            failedRun.CompletedAt = DateTimeOffset.UtcNow;
            failedRun.Error = ex.Message;
            failedRun.Summary = "Analysis failed. The error is visible in the run details.";
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task CompleteFromResultAsync(AnalysisRun run, string artifactDirectory, string resultPath, CancellationToken ct)
    {
        logger.LogInformation("Analysis run {AnalysisRunId} detected worker result at {ResultPath}.", run.Id, resultPath);
        run.ResultJson = await File.ReadAllTextAsync(resultPath, ct);
        run.ToolVersion = TryReadString(run.ResultJson, "tool_version") ?? "veritas_forensics";
        run.Summary = TryReadString(run.ResultJson, "summary") ?? "Forensic worker completed.";
        run.Status = AnalysisStatus.Completed;
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.Error = null;

        var existingArtifacts = await db.AnalysisArtifacts.Where(x => x.AnalysisRunId == run.Id).ToListAsync(ct);
        var existingFindings = await db.Findings.Where(x => x.AnalysisRunId == run.Id).ToListAsync(ct);
        db.AnalysisArtifacts.RemoveRange(existingArtifacts);
        db.Findings.RemoveRange(existingFindings);

        logger.LogInformation("Analysis run {AnalysisRunId} is recording artifacts and findings.", run.Id);
        AddArtifacts(run, artifactDirectory);
        AddFindings(run);

        db.ChainOfCustodyEvents.Add(new ChainOfCustodyEvent
        {
            EvidenceItemId = run.EvidenceItemId,
            EventType = CustodyEventType.Analyzed,
            Actor = "system",
            DetailsJson = JsonSerializer.Serialize(new { run.Id, run.Pipeline })
        });
        db.TimelineEntries.Add(new TimelineEntry
        {
            DossierId = run.EvidenceItem.DossierId,
            Time = DateTimeOffset.UtcNow,
            Source = run.EvidenceItem.Title,
            EvidenceHash = run.EvidenceItem.ContentHashSha256,
            Caption = $"Analysis completed: {run.Pipeline} on {run.EvidenceItem.Title}",
            Notes = run.Summary,
            Confidence = ConfidenceLevel.Medium
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Analysis run {AnalysisRunId} completed and was saved.", run.Id);
    }

    private string ResolveWorkerDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_options.WorkerDirectory))
        {
            return Path.GetFullPath(_options.WorkerDirectory);
        }

        var current = Directory.GetCurrentDirectory();
        for (var i = 0; i < 6; i++)
        {
            var candidate = Path.GetFullPath(Path.Combine(current, string.Concat(Enumerable.Repeat("..", i).Select(x => x + Path.DirectorySeparatorChar)), "workers", "forensics"));
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        return current;
    }

    private void AddArtifacts(AnalysisRun run, string artifactDirectory)
    {
        foreach (var file in Directory.EnumerateFiles(artifactDirectory, "*", SearchOption.AllDirectories))
        {
            var filename = Path.GetFileName(file);
            var relative = Path.GetRelativePath(artifactDirectory, file).Replace('\\', '/');
            var storageKey = artifactStorage.GetArtifactStorageKey(run.Id, relative);
            db.AnalysisArtifacts.Add(new AnalysisArtifact
            {
                AnalysisRunId = run.Id,
                EvidenceItemId = run.EvidenceItemId,
                Filename = filename,
                StorageKey = storageKey,
                ContentType = GuessContentType(filename),
                ArtifactType = Path.GetExtension(filename).TrimStart('.').ToLowerInvariant()
            });
        }
    }

    private void AddFindings(AnalysisRun run)
    {
        if (string.IsNullOrWhiteSpace(run.ResultJson) || run.EvidenceItem is null)
        {
            return;
        }

        using var doc = JsonDocument.Parse(run.ResultJson);
        if (!doc.RootElement.TryGetProperty("findings", out var findings) || findings.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var item in findings.EnumerateArray())
        {
            var category = ParseEnum(item, "category", FindingCategory.Other);
            var confidence = ParseEnum(item, "confidence", ConfidenceLevel.None);
            var direction = ParseEnum(item, "direction", FindingDirection.Inconclusive);
            var claim = Read(item, "claim", "Worker produced an inconclusive forensic observation.");

            db.Findings.Add(new Finding
            {
                DossierId = run.EvidenceItem.DossierId,
                EvidenceItemId = run.EvidenceItemId,
                AnalysisRunId = run.Id,
                Category = category,
                Confidence = confidence,
                Direction = direction,
                Claim = claim,
                Evidence = Read(item, "evidence", "See analysis result JSON for details."),
                Limitations = Read(item, "limitations", "This observation is not sufficient for attribution or identity claims."),
                FalsificationPath = Read(item, "falsification_path", "Provide original source media and independent chronology for comparison.")
            });
        }
    }

    private static TEnum ParseEnum<TEnum>(JsonElement item, string name, TEnum fallback) where TEnum : struct
    {
        var value = Read(item, name, "");
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;
    }

    private static string Read(JsonElement item, string name, string fallback)
    {
        return item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static string? TryReadString(string json, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string GuessContentType(string filename)
    {
        return Path.GetExtension(filename).ToLowerInvariant() switch
        {
            ".json" => "application/json",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }
}
