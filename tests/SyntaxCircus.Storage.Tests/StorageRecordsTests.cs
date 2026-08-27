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

public class StorageObjectMetadataTests
{
    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var lastModified = new DateTimeOffset(2026, 8, 26, 12, 30, 0, TimeSpan.Zero);

        var metadata = new StorageObjectMetadata("widgets/one.bin", 42L, "application/octet-stream", lastModified);

        metadata.Key.ShouldBe("widgets/one.bin");
        metadata.SizeBytes.ShouldBe(42L);
        metadata.ContentType.ShouldBe("application/octet-stream");
        metadata.LastModified.ShouldBe(lastModified);
    }
}

public class ListStorageObjectsRequestTests
{
    [Fact]
    public void Ctor_DefaultsContinuationAndPageSize()
    {
        var request = new ListStorageObjectsRequest("widgets/");

        request.Prefix.ShouldBe("widgets/");
        request.AfterKey.ShouldBeNull();
        request.PageSize.ShouldBe(100);
    }

    [Fact]
    public void Ctor_SetsAllProperties()
    {
        var request = new ListStorageObjectsRequest("widgets/", "widgets/one.bin", 25);

        request.Prefix.ShouldBe("widgets/");
        request.AfterKey.ShouldBe("widgets/one.bin");
        request.PageSize.ShouldBe(25);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public void Ctor_AcceptsPageSizeBounds(int pageSize)
    {
        var request = new ListStorageObjectsRequest("widgets/", PageSize: pageSize);

        request.PageSize.ShouldBe(pageSize);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public void Ctor_RejectsPageSizeOutsideBounds(int pageSize)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new ListStorageObjectsRequest("widgets/", PageSize: pageSize));
    }
}

public class StorageObjectPageTests
{
    [Fact]
    public void Ctor_SetsItemsAndContinuation()
    {
        var item = new StorageObjectMetadata("widgets/one.bin", 42L, null, DateTimeOffset.UnixEpoch);
        IReadOnlyList<StorageObjectMetadata> items = [item];

        var page = new StorageObjectPage(items, "widgets/one.bin");

        page.Items.ShouldBeSameAs(items);
        page.NextAfterKey.ShouldBe("widgets/one.bin");
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

    [Fact]
    public async Task GetMetadataAsync_NotOverridden_ThrowsNotSupportedException()
    {
        IStorageProvider provider = new NoOpStorageProvider();

        await Should.ThrowAsync<NotSupportedException>(
            () => provider.GetMetadataAsync("key.txt", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_NotOverridden_ThrowsNotSupportedException()
    {
        IStorageProvider provider = new NoOpStorageProvider();

        await Should.ThrowAsync<NotSupportedException>(
            () => provider.ListAsync(new ListStorageObjectsRequest(""), TestContext.Current.CancellationToken));
    }
}
