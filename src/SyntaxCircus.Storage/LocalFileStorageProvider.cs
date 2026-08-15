namespace SyntaxCircus.Storage;

public sealed class LocalFileStorageProvider(IOptions<LocalStorageOptions> options) : IStorageProvider
{
    public async Task<StoredObject> StoreAsync(StoreObjectRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var path = ResolvePath(request.Key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var fileStream = File.Create(path);
        await request.Content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);

        return new StoredObject(request.Key, fileStream.Length);
    }

    public Task<StorageReadResult?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (!File.Exists(path))
        {
            return Task.FromResult<StorageReadResult?>(null);
        }

        var fileInfo = new FileInfo(path);
        var stream = File.OpenRead(path);
        return Task.FromResult<StorageReadResult?>(new StorageReadResult(stream, ContentType: null, fileInfo.Length));
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(ResolvePath(key)));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string ResolvePath(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        // Reject path traversal / absolute paths — a key is always relative to RootPath.
        var normalizedKey = key.Replace('\\', '/').TrimStart('/');
        if (normalizedKey.Split('/').Any(segment => segment is ".." or "."))
        {
            throw new ArgumentException("Storage key must not contain path traversal segments.", nameof(key));
        }

        return Path.GetFullPath(Path.Combine(options.Value.RootPath, normalizedKey));
    }
}
