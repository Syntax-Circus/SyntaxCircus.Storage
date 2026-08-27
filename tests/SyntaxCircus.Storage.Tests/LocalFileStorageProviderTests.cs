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

    [Theory]
    [InlineData("C:/Windows/evil.txt")]
    [InlineData(@"C:\Windows\evil.txt")]
    [InlineData("C:evil.txt")]
    [InlineData("D:/other-drive/evil.txt")]
    public async Task StoreAsync_DriveRootedKey_ThrowsArgumentException(string key)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var content = ContentStream("x");

        await Should.ThrowAsync<ArgumentException>(
            () => _provider.StoreAsync(new StoreObjectRequest(key, content), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StoreAsync_UncKey_TreatedAsRelativeUnderRoot()
    {
        using var content = ContentStream("x");

        await _provider.StoreAsync(new StoreObjectRequest(@"\\server\share\file.txt", content), TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(_rootPath, "server", "share", "file.txt")).ShouldBeTrue();
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

    [Fact]
    public async Task GetMetadataAsync_ExistingFile_ReturnsNormalizedKeySizeAndUtcLastModified()
    {
        using var content = ContentStream("metadata");
        var beforeWrite = DateTimeOffset.UtcNow.AddSeconds(-1);
        await _provider.StoreAsync(new StoreObjectRequest(@"widgets\file.txt", content, "text/plain"), TestContext.Current.CancellationToken);

        var metadata = await _provider.GetMetadataAsync("widgets/file.txt", TestContext.Current.CancellationToken);

        metadata.ShouldNotBeNull();
        metadata.Key.ShouldBe("widgets/file.txt");
        metadata.SizeBytes.ShouldBe(8L);
        metadata.ContentType.ShouldBeNull();
        metadata.LastModified.Offset.ShouldBe(TimeSpan.Zero);
        metadata.LastModified.ShouldBeGreaterThanOrEqualTo(beforeWrite);
        metadata.LastModified.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task GetMetadataAsync_MissingFile_ReturnsNull()
    {
        var metadata = await _provider.GetMetadataAsync("missing.txt", TestContext.Current.CancellationToken);

        metadata.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsOrdinalPrefixPagesWithStableAfterKey()
    {
        foreach (var key in new[] { "widgets/c.txt", "other.txt", "widgets/a.txt", "widgets/b.txt" })
        {
            using var content = ContentStream(key);
            await _provider.StoreAsync(new StoreObjectRequest(key, content), TestContext.Current.CancellationToken);
        }

        var first = await _provider.ListAsync(new ListStorageObjectsRequest("widgets/", PageSize: 2), TestContext.Current.CancellationToken);
        var second = await _provider.ListAsync(new ListStorageObjectsRequest("widgets/", first.NextAfterKey, 2), TestContext.Current.CancellationToken);

        first.Items.Select(item => item.Key).ShouldBe(["widgets/a.txt", "widgets/b.txt"]);
        first.NextAfterKey.ShouldBe("widgets/b.txt");
        second.Items.Select(item => item.Key).ShouldBe(["widgets/c.txt"]);
        second.NextAfterKey.ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_EmptyAndBackslashPrefixesBehaveAsNormalizedPrefixes()
    {
        foreach (var key in new[] { "widgets/a.txt", "other.txt" })
        {
            using var content = ContentStream(key);
            await _provider.StoreAsync(new StoreObjectRequest(key, content), TestContext.Current.CancellationToken);
        }

        var all = await _provider.ListAsync(new ListStorageObjectsRequest(""), TestContext.Current.CancellationToken);
        var widgets = await _provider.ListAsync(new ListStorageObjectsRequest(@"widgets\"), TestContext.Current.CancellationToken);
        var missing = await _provider.ListAsync(new ListStorageObjectsRequest("missing/"), TestContext.Current.CancellationToken);

        all.Items.Select(item => item.Key).ShouldBe(["other.txt", "widgets/a.txt"]);
        widgets.Items.Select(item => item.Key).ShouldBe(["widgets/a.txt"]);
        missing.Items.ShouldBeEmpty();
        missing.NextAfterKey.ShouldBeNull();
    }

    [Theory]
    [InlineData("../")]
    [InlineData("a/../../")]
    [InlineData("./")]
    [InlineData("a/./")]
    public async Task ListAsync_TraversalPrefix_ThrowsArgumentException(string prefix)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _provider.ListAsync(new ListStorageObjectsRequest(prefix), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_DriveRootedPrefix_ThrowsArgumentExceptionOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        await Should.ThrowAsync<ArgumentException>(
            () => _provider.ListAsync(new ListStorageObjectsRequest("C:/Windows/"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NewMetadataOperations_PreCanceledToken_ThrowOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => _provider.GetMetadataAsync("file.txt", cancellation.Token));
        await Should.ThrowAsync<OperationCanceledException>(() => _provider.ListAsync(new ListStorageObjectsRequest(""), cancellation.Token));
    }

    [Fact]
    public async Task GetAccessUrlAsync_PublicBaseUrlConfigured_ReturnsExpectedUrl()
    {
        var provider = new LocalFileStorageProvider(Options.Create(new LocalStorageOptions
        {
            RootPath = _rootPath,
            PublicBaseUrl = "https://cdn.example.com/files/",
        }));

        var url = await provider.GetAccessUrlAsync("a/b/file.txt", cancellationToken: TestContext.Current.CancellationToken);

        url.ShouldBe("https://cdn.example.com/files/a/b/file.txt");
    }

    [Fact]
    public async Task GetAccessUrlAsync_PublicBaseUrlNotConfigured_ThrowsInvalidOperationException()
    {
        await Should.ThrowAsync<InvalidOperationException>(
            () => _provider.GetAccessUrlAsync("file.txt", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAccessUrlAsync_PathTraversalSegment_ThrowsArgumentException()
    {
        var provider = new LocalFileStorageProvider(Options.Create(new LocalStorageOptions
        {
            RootPath = _rootPath,
            PublicBaseUrl = "https://cdn.example.com/files",
        }));

        await Should.ThrowAsync<ArgumentException>(
            () => provider.GetAccessUrlAsync("../escape.txt", cancellationToken: TestContext.Current.CancellationToken));
    }
}
