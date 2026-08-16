using System.Text;

namespace SyntaxCircus.Storage.Tests;

public sealed class LocalFileStorageProviderTests : IDisposable
{
    private readonly string _rootPath;
    private readonly LocalFileStorageProvider _provider;

    public LocalFileStorageProviderTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "sc-storage-tests", Guid.NewGuid().ToString("N"));
        _provider = new LocalFileStorageProvider(Options.Create(new LocalStorageOptions { RootPath = _rootPath }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }

    private static MemoryStream ContentStream(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task StoreAsync_FlatKey_CreatesFileWithCorrectSize()
    {
        using var content = ContentStream("hello world");

        var result = await _provider.StoreAsync(new StoreObjectRequest("file.txt", content), TestContext.Current.CancellationToken);

        result.Key.ShouldBe("file.txt");
        result.SizeBytes.ShouldBe(11L);
        File.Exists(Path.Combine(_rootPath, "file.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task StoreAsync_NestedKey_CreatesIntermediateDirectories()
    {
        using var content = ContentStream("nested");

        await _provider.StoreAsync(new StoreObjectRequest("a/b/c.txt", content), TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(_rootPath, "a", "b", "c.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task StoreAsync_ExistingKey_OverwritesContent()
    {
        using (var first = ContentStream("original content here"))
        {
            await _provider.StoreAsync(new StoreObjectRequest("file.txt", first), TestContext.Current.CancellationToken);
        }

        using (var second = ContentStream("new"))
        {
            await _provider.StoreAsync(new StoreObjectRequest("file.txt", second), TestContext.Current.CancellationToken);
        }

        var written = await File.ReadAllTextAsync(Path.Combine(_rootPath, "file.txt"), TestContext.Current.CancellationToken);
        written.ShouldBe("new");
    }

    [Fact]
    public async Task StoreAsync_NullRequest_ThrowsArgumentNullException()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => _provider.StoreAsync(null!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("a/../../escape.txt")]
    [InlineData("./file.txt")]
    [InlineData("a/./b.txt")]
    public async Task StoreAsync_PathTraversalSegment_ThrowsArgumentException(string key)
    {
        using var content = ContentStream("x");

        await Should.ThrowAsync<ArgumentException>(
            () => _provider.StoreAsync(new StoreObjectRequest(key, content), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StoreAsync_BlankKey_ThrowsArgumentException(string key)
    {
        using var content = ContentStream("x");

        await Should.ThrowAsync<ArgumentException>(
            () => _provider.StoreAsync(new StoreObjectRequest(key, content), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StoreAsync_BackslashSeparators_NormalizedToNestedPath()
    {
        using var content = ContentStream("x");

        await _provider.StoreAsync(new StoreObjectRequest(@"a\b\c.txt", content), TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(_rootPath, "a", "b", "c.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task StoreAsync_LeadingSlashKey_TreatedAsRelativeUnderRoot()
    {
        using var content = ContentStream("x");

        await _provider.StoreAsync(new StoreObjectRequest("/file.txt", content), TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(_rootPath, "file.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task ReadAsync_ExistingFile_ReturnsContentAndSize()
    {
        using (var content = ContentStream("read me"))
        {
            await _provider.StoreAsync(new StoreObjectRequest("file.txt", content), TestContext.Current.CancellationToken);
        }

        await using var result = await _provider.ReadAsync("file.txt", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.SizeBytes.ShouldBe(7L);
        result.ContentType.ShouldBeNull();

        using var reader = new StreamReader(result.Content);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("read me");
    }

    [Fact]
    public async Task ReadAsync_MissingFile_ReturnsNull()
    {
        var result = await _provider.ReadAsync("missing.txt", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExistsAsync_ExistingFile_ReturnsTrue()
    {
        using var content = ContentStream("x");
        await _provider.StoreAsync(new StoreObjectRequest("file.txt", content), TestContext.Current.CancellationToken);

        (await _provider.ExistsAsync("file.txt", TestContext.Current.CancellationToken)).ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_MissingFile_ReturnsFalse()
    {
        (await _provider.ExistsAsync("missing.txt", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ExistingFile_RemovesIt()
    {
        using var content = ContentStream("x");
        await _provider.StoreAsync(new StoreObjectRequest("file.txt", content), TestContext.Current.CancellationToken);

        await _provider.DeleteAsync("file.txt", TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(_rootPath, "file.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_MissingFile_DoesNotThrow()
    {
        await Should.NotThrowAsync(() => _provider.DeleteAsync("missing.txt", TestContext.Current.CancellationToken));
    }
}
