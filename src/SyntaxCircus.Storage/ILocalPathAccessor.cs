namespace SyntaxCircus.Storage;

/// <summary>
/// Optional capability for an <see cref="IStorageProvider"/> that can resolve a stored object to a
/// real path on the local filesystem. Only providers actually backed by local disk storage should
/// implement this (e.g. <see cref="LocalFileStorageProvider"/>) — cloud-backed providers like
/// <see cref="S3StorageProvider"/> have no local path to offer and must not implement it.
/// </summary>
/// <remarks>
/// This is deliberately a separate, optional interface rather than a member of
/// <see cref="IStorageProvider"/> so the core storage contract stays provider-agnostic and no
/// implementation is forced to support local-path access. Consumers that need a local path should
/// pattern-match: <c>if (storageProvider is ILocalPathAccessor localPathAccessor) { ... }</c>.
/// </remarks>
public interface ILocalPathAccessor
{
    /// <summary>
    /// Gets the real filesystem path for the object stored under <paramref name="key"/>,
    /// or <see langword="null"/> when the key does not exist.
    /// </summary>
    Task<string?> GetLocalPathAsync(string key, CancellationToken cancellationToken = default);
}
