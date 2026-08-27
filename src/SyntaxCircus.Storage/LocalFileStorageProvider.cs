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

    public Task<StorageObjectMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedKey = NormalizeKey(key);
        var path = ResolvePath(normalizedKey);
        if (!File.Exists(path))
        {
            return Task.FromResult<StorageObjectMetadata?>(null);
        }

        return Task.FromResult<StorageObjectMetadata?>(CreateMetadata(path, normalizedKey));
    }

    public Task<StorageObjectPage> ListAsync(ListStorageObjectsRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var prefix = NormalizePrefix(request.Prefix);
        var afterKey = request.AfterKey?.Replace('\\', '/');
        var root = GetRootPath();
        if (!Directory.Exists(root))
        {
            return Task.FromResult(new StorageObjectPage([], null));
        }

        var candidates = new SortedSet<StorageObjectMetadata>(
            Comparer<StorageObjectMetadata>.Create((left, right) => StringComparer.Ordinal.Compare(left.Key, right.Key)));
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var path in Directory.EnumerateFiles(root, "*", enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedKey = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!normalizedKey.StartsWith(prefix, StringComparison.Ordinal) ||
                (afterKey is not null && string.Compare(normalizedKey, afterKey, StringComparison.Ordinal) <= 0))
            {
                continue;
            }

            candidates.Add(CreateMetadata(path, normalizedKey));
            if (candidates.Count > request.PageSize + 1)
            {
                candidates.Remove(candidates.Max!);
            }
        }

        var ordered = candidates.ToArray();
        var hasMore = ordered.Length > request.PageSize;
        var items = hasMore ? ordered[..request.PageSize] : ordered;
        var nextAfterKey = hasMore ? items[^1].Key : null;
        return Task.FromResult(new StorageObjectPage(items, nextAfterKey));
    }

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
        var normalizedKey = NormalizeKey(key);
        var root = GetRootPath();

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

    private string NormalizePrefix(string prefix)
    {
        var normalizedPrefix = prefix.Replace('\\', '/').TrimStart('/');
        if (normalizedPrefix.Split('/').Any(segment => segment is ".." or "."))
        {
            throw new ArgumentException("Storage prefix must not contain path traversal segments.", nameof(prefix));
        }

        if (normalizedPrefix.Length > 0)
        {
            _ = ResolveContainedPath(normalizedPrefix, nameof(prefix));
        }

        return normalizedPrefix;
    }

    private static string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        var normalizedKey = key.Replace('\\', '/').TrimStart('/');
        if (normalizedKey.Split('/').Any(segment => segment is ".." or "."))
        {
            throw new ArgumentException("Storage key must not contain path traversal segments.", nameof(key));
        }

        return normalizedKey;
    }

    private string ResolveContainedPath(string normalizedKey, string parameterName)
    {
        var root = GetRootPath();
        var resolved = Path.GetFullPath(Path.Combine(root, normalizedKey));
        if (!resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            (resolved.Length > root.Length && resolved[root.Length] != Path.DirectorySeparatorChar))
        {
            throw new ArgumentException("Storage path resolves outside the configured storage root.", parameterName);
        }

        return resolved;
    }

    private string GetRootPath() => Path.GetFullPath(options.Value.RootPath);

    private static StorageObjectMetadata CreateMetadata(string path, string normalizedKey)
    {
        var fileInfo = new FileInfo(path);
        return new StorageObjectMetadata(normalizedKey, fileInfo.Length, ContentType: null, new DateTimeOffset(fileInfo.LastWriteTimeUtc));
    }
}
