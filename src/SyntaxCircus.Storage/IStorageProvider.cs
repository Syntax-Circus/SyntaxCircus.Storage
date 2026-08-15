namespace SyntaxCircus.Storage;

public interface IStorageProvider
{
    Task<StoredObject> StoreAsync(StoreObjectRequest request, CancellationToken cancellationToken = default);

    Task<StorageReadResult?> ReadAsync(string key, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}

public sealed record StoreObjectRequest(string Key, Stream Content, string? ContentType = null);

public sealed record StoredObject(string Key, long SizeBytes);

public sealed record StorageReadResult(Stream Content, string? ContentType, long SizeBytes) : IAsyncDisposable
{
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}
