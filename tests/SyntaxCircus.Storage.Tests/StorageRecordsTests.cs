namespace SyntaxCircus.Storage.Tests;

public class StoreObjectRequestTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        using var content = new MemoryStream();

        var request = new StoreObjectRequest("key.txt", content, "text/plain");

        request.Key.ShouldBe("key.txt");
        request.Content.ShouldBeSameAs(content);
        request.ContentType.ShouldBe("text/plain");
    }

    [Fact]
    public void Ctor_ContentTypeDefaultsToNull()
    {
        using var content = new MemoryStream();

        var request = new StoreObjectRequest("key.txt", content);

        request.ContentType.ShouldBeNull();
    }
}

public class StoredObjectTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var stored = new StoredObject("key.txt", 42L);

        stored.Key.ShouldBe("key.txt");
        stored.SizeBytes.ShouldBe(42L);
    }
}

public class StorageReadResultTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        using var content = new MemoryStream();

        var result = new StorageReadResult(content, "text/plain", 10L);

        result.Content.ShouldBeSameAs(content);
        result.ContentType.ShouldBe("text/plain");
        result.SizeBytes.ShouldBe(10L);
    }

    [Fact]
    public async Task DisposeAsync_DisposesContentStream()
    {
        var content = new MemoryStream();
        var result = new StorageReadResult(content, null, 0L);

        await result.DisposeAsync();

        Should.Throw<ObjectDisposedException>(() => content.ReadByte());
    }
}

public class IStorageProviderDefaultsTests
{
    /// <summary>A minimal implementation that doesn't override <c>GetAccessUrlAsync</c>, to prove the interface's default member behaves correctly.</summary>
    private sealed class NoOpStorageProvider : IStorageProvider
    {
        public Task<StoredObject> StoreAsync(StoreObjectRequest request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<StorageReadResult?> ReadAsync(string key, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    [Fact]
    public async Task GetAccessUrlAsync_NotOverridden_ThrowsNotSupportedException()
    {
        IStorageProvider provider = new NoOpStorageProvider();

        await Should.ThrowAsync<NotSupportedException>(
            () => provider.GetAccessUrlAsync("key.txt", cancellationToken: TestContext.Current.CancellationToken));
    }
}
