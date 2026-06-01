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

        run.Status = AnalysisStatus.Running;
        run.StartedAt = DateTimeOffset.UtcNow;
        run.Summary = "Forensic worker is running.";
        await db.SaveChangesAsync(ct);

        var artifactDirectory = artifactStorage.GetArtifactDirectory(run.Id);
        var resultPath = Path.Combine(artifactDirectory, "result.json");
        var inputPath = artifactStorage.GetAbsolutePath(run.EvidenceItem.StoragePath);

        try
        {
            var workerDirectory = ResolveWorkerDirectory();
            var psi = new ProcessStartInfo
            {
                FileName = _options.PythonExecutable,
                WorkingDirectory = workerDirectory,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
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
            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            var timeout = TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds));
            var exited = await Task.Run(() => process.WaitForExit((int)timeout.TotalMilliseconds), ct);
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (!exited)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"Forensic worker exceeded timeout of {_options.TimeoutSeconds} seconds.");
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException($"Forensic worker exited with code {process.ExitCode}. {stderr}".Trim());
            }

            if (!File.Exists(resultPath))
            {
                throw new FileNotFoundException("Forensic worker did not write result.json.", resultPath);
            }

            run.ResultJson = await File.ReadAllTextAsync(resultPath, ct);
            run.ToolVersion = TryReadString(run.ResultJson, "tool_version") ?? "veritas_forensics";
            run.Summary = TryReadString(run.ResultJson, "summary") ?? stdout.Trim();
            run.Status = AnalysisStatus.Completed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.Error = null;

            AddArtifacts(run, artifactDirectory);
            AddFindings(run);

            db.ChainOfCustodyEvents.Add(new ChainOfCustodyEvent
            {
                EvidenceItemId = run.EvidenceItemId,
                EventType = CustodyEventType.Analyzed,
                Actor = "system",
                DetailsJson = JsonSerializer.Serialize(new { run.Id, run.Pipeline })
            });

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Analysis run {AnalysisRunId} failed.", run.Id);
            run.Status = AnalysisStatus.Failed;
            run.CompletedAt = DateTimeOffset.UtcNow;
            run.Error = ex.Message;
            run.Summary = "Analysis failed. The error is visible in the run details.";
            await db.SaveChangesAsync(ct);
        }
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
            run.Artifacts.Add(new AnalysisArtifact
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

            run.Findings.Add(new Finding
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
