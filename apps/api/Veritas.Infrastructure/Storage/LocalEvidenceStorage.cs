using Microsoft.Extensions.Options;
using Veritas.Application.Storage;

namespace Veritas.Infrastructure.Storage;

public sealed class LocalEvidenceStorage(IOptions<StorageOptions> options) : IEvidenceStorage, IArtifactStorage
{
    private readonly string _rootPath = Path.GetFullPath(options.Value.RootPath);

    public async Task<StoredObject> SaveAsync(Stream content, string filename, string contentType, CancellationToken ct)
    {
        var safeName = SanitizeFilename(filename);
        var extension = Path.GetExtension(safeName);
        var storageKey = Path.Combine("originals", DateTimeOffset.UtcNow.ToString("yyyy"), DateTimeOffset.UtcNow.ToString("MM"), $"{Guid.NewGuid():N}{extension}")
            .Replace('\\', '/');
        var destination = GetAbsolutePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        await using var output = File.Create(destination);
        await content.CopyToAsync(output, ct);

        return new StoredObject(storageKey, safeName, contentType, output.Length, await GetDownloadUriAsync(storageKey, ct));
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var path = GetAbsolutePath(storageKey);
        Stream stream = File.OpenRead(path);
        return Task.FromResult(stream);
    }

    public Task<Uri> GetDownloadUriAsync(string storageKey, CancellationToken ct)
    {
        var escaped = Uri.EscapeDataString(storageKey);
        return Task.FromResult(new Uri($"/api/storage/{escaped}", UriKind.Relative));
    }

    public string GetAbsolutePath(string storageKey)
    {
        var combined = Path.GetFullPath(Path.Combine(_rootPath, storageKey.Replace('/', Path.DirectorySeparatorChar)));
        if (!combined.StartsWith(_rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Storage key resolves outside the configured storage root.");
        }

        return combined;
    }

    public string GetArtifactDirectory(Guid analysisRunId)
    {
        var directory = GetAbsolutePath(Path.Combine("artifacts", analysisRunId.ToString("N")).Replace('\\', '/'));
        Directory.CreateDirectory(directory);
        return directory;
    }

    public string GetArtifactStorageKey(Guid analysisRunId, string filename)
    {
        return Path.Combine("artifacts", analysisRunId.ToString("N"), SanitizeFilename(filename)).Replace('\\', '/');
    }

    private static string SanitizeFilename(string filename)
    {
        var name = string.IsNullOrWhiteSpace(filename) ? "upload.bin" : Path.GetFileName(filename);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }
}
