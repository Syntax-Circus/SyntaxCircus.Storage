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

    public Task<string> GetAccessUrlAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
    {
        // Validates the key with the same traversal/rooted-path guard StoreAsync/ReadAsync use,
        // even though nothing is read from disk here.
        ResolvePath(key);

        var baseUrl = options.Value.PublicBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException(
                $"{nameof(LocalStorageOptions)}.{nameof(LocalStorageOptions.PublicBaseUrl)} must be configured to build access URLs.");
        }

        // expiry is intentionally ignored — local-disk URLs aren't signed/time-limited.
        var normalizedKey = key.Replace('\\', '/').TrimStart('/');
        return Task.FromResult($"{baseUrl.TrimEnd('/')}/{normalizedKey}");
    }

    private string ResolvePath(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var root = Path.GetFullPath(options.Value.RootPath);

        // Reject path traversal segments — a key is always relative to RootPath.
        var normalizedKey = key.Replace('\\', '/').TrimStart('/');
        if (normalizedKey.Split('/').Any(segment => segment is ".." or "."))
        {
            throw new ArgumentException("Storage key must not contain path traversal segments.", nameof(key));
        }

        // Re-validate containment after full resolution: a rooted key (e.g. a Windows drive-absolute
        // or drive-relative path) survives the checks above untouched and would otherwise cause
        // Path.Combine to discard `root` entirely, escaping RootPath.
        var resolved = Path.GetFullPath(Path.Combine(root, normalizedKey));
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            (resolved.Length > root.Length && resolved[root.Length] != Path.DirectorySeparatorChar))
        {
            throw new ArgumentException("Storage key resolves outside the configured storage root.", nameof(key));
        }

        return resolved;
    }
}
