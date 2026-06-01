namespace Veritas.Application.Storage;

public sealed record StoredObject(
    string StorageKey,
    string OriginalFilename,
    string ContentType,
    long SizeBytes,
    Uri DownloadUri);

public interface IEvidenceStorage
{
    Task<StoredObject> SaveAsync(Stream content, string filename, string contentType, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
    Task<Uri> GetDownloadUriAsync(string storageKey, CancellationToken ct);
}

public interface IArtifactStorage
{
    string GetAbsolutePath(string storageKey);
    string GetArtifactDirectory(Guid analysisRunId);
    string GetArtifactStorageKey(Guid analysisRunId, string filename);
}
