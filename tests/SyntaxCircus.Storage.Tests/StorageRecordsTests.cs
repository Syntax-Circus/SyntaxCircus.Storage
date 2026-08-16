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
