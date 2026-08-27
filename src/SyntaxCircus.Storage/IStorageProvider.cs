namespace SyntaxCircus.Storage;

public interface IStorageProvider
{
    Task<StoredObject> StoreAsync(StoreObjectRequest request, CancellationToken cancellationToken = default);

    Task<StorageReadResult?> ReadAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metadata for an object, or <see langword="null"/> when the key does not exist.
    /// </summary>
    Task<StorageObjectMetadata?> GetMetadataAsync(string key, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"{GetType().Name} does not support {nameof(GetMetadataAsync)}.");

    /// <summary>
    /// Lists a bounded page of object metadata in ordinal key order.
    /// </summary>
    Task<StorageObjectPage> ListAsync(ListStorageObjectsRequest request, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"{GetType().Name} does not support {nameof(ListAsync)}.");

    /// <summary>
    /// Builds a URL a client can use to access <paramref name="key"/> directly, bypassing your app.
    /// <paramref name="expiry"/> is a hint for providers that support time-limited/signed URLs (e.g.
    /// cloud object storage) — implementations that can't honor it (like <see cref="LocalFileStorageProvider"/>)
    /// ignore it and return a stable URL instead.
    /// </summary>
    /// <remarks>
    /// Added after the interface's initial release as a default interface method so existing
    /// implementations keep compiling unchanged. The default throws <see cref="NotSupportedException"/>;
    /// override it in your own <see cref="IStorageProvider"/> if it can build access URLs.
    /// </remarks>
    Task<string> GetAccessUrlAsync(string key, TimeSpan? expiry = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException($"{GetType().Name} does not support {nameof(GetAccessUrlAsync)}.");
}

public sealed record StoreObjectRequest(string Key, Stream Content, string? ContentType = null);

public sealed record StoredObject(string Key, long SizeBytes);

public sealed record StorageObjectMetadata(string Key, long SizeBytes, string? ContentType, DateTimeOffset LastModified);

public sealed record ListStorageObjectsRequest
{
    public ListStorageObjectsRequest(string Prefix, string? AfterKey = null, int PageSize = 100)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(PageSize, 1_000);

        this.Prefix = Prefix;
        this.AfterKey = AfterKey;
        this.PageSize = PageSize;
    }

    public string Prefix { get; }

    public string? AfterKey { get; }

    public int PageSize { get; }
}

public sealed record StorageObjectPage(IReadOnlyList<StorageObjectMetadata> Items, string? NextAfterKey);

public sealed record StorageReadResult(Stream Content, string? ContentType, long SizeBytes) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
