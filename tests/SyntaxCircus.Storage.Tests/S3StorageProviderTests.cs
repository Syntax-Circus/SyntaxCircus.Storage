using System.Net;
using System.Text;
using Amazon.S3;
using Amazon.S3.Model;
using NSubstitute;
using Testcontainers.Minio;

namespace SyntaxCircus.Storage.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class MinioTestGroup : ICollectionFixture<MinioFixture>
{
    public const string Name = "MinIO";
}

public sealed class MinioFixture : IAsyncLifetime
{
    private readonly MinioContainer container = new MinioBuilder("minio/minio:RELEASE.2025-04-22T22-12-26Z").Build();

    public string BucketName { get; } = $"storage-tests-{Guid.NewGuid():N}";

    public IAmazonS3 Client { get; private set; } = null!;

    public S3StorageOptions Options { get; private set; } = null!;

    public ValueTask InitializeAsync() => new(InitializeCoreAsync());

    private async Task InitializeCoreAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);
        Options = new S3StorageOptions
        {
            BucketName = BucketName,
            Region = "us-east-1",
            ServiceUrl = container.GetConnectionString(),
            AccessKey = container.GetAccessKey(),
            SecretKey = container.GetSecretKey(),
            ForcePathStyle = true
        };
        Client = new AmazonS3Client(Options.AccessKey, Options.SecretKey, new AmazonS3Config
        {
            ServiceURL = Options.ServiceUrl,
            AuthenticationRegion = Options.Region,
            ForcePathStyle = Options.ForcePathStyle
        });
        await Client.PutBucketAsync(new PutBucketRequest { BucketName = BucketName }, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await container.DisposeAsync();
    }
}

[Collection(MinioTestGroup.Name)]
public sealed class S3StorageProviderMinioTests(MinioFixture fixture)
{
    private S3StorageProvider CreateProvider() => new(fixture.Client, Options.Create(fixture.Options));

    [Fact]
    public async Task StoreReadAndMetadata_PreserveCallerKeyContentTypeAndSize()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello minio"));
        var key = $"roundtrip/{Guid.NewGuid():N}/hello.txt";
        using var provider = CreateProvider();

        var stored = await provider.StoreAsync(
            new StoreObjectRequest(key, content, "text/plain"), TestContext.Current.CancellationToken);
        await using var read = await provider.ReadAsync(key, TestContext.Current.CancellationToken);
        var metadata = await provider.GetMetadataAsync(key, TestContext.Current.CancellationToken);

        stored.ShouldBe(new StoredObject(key, 11));
        read.ShouldNotBeNull();
        read.ContentType.ShouldBe("text/plain");
        read.SizeBytes.ShouldBe(11);
        using var reader = new StreamReader(read.Content, Encoding.UTF8);
        (await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).ShouldBe("hello minio");
        metadata.ShouldNotBeNull();
        metadata.Key.ShouldBe(key);
        metadata.ContentType.ShouldBe("text/plain");
        metadata.SizeBytes.ShouldBe(11);
        metadata.LastModified.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task ListAsync_ReturnsBoundedOrdinalPagesUsingAfterKey()
    {
        var prefix = $"paging/{Guid.NewGuid():N}/";
        using var provider = CreateProvider();
        foreach (var name in new[] { "c.bin", "a.bin", "b.bin", "outside.bin" })
        {
            var key = name == "outside.bin" ? $"other/{Guid.NewGuid():N}.bin" : prefix + name;
            using var content = new MemoryStream([1, 2, 3]);
            await provider.StoreAsync(new StoreObjectRequest(key, content), TestContext.Current.CancellationToken);
        }

        var first = await provider.ListAsync(
            new ListStorageObjectsRequest(prefix, PageSize: 2), TestContext.Current.CancellationToken);
        var second = await provider.ListAsync(
            new ListStorageObjectsRequest(prefix, first.NextAfterKey, 2), TestContext.Current.CancellationToken);

        first.Items.Select(item => item.Key).ShouldBe([prefix + "a.bin", prefix + "b.bin"]);
        first.NextAfterKey.ShouldBe(prefix + "b.bin");
        second.Items.Select(item => item.Key).ShouldBe([prefix + "c.bin"]);
        second.NextAfterKey.ShouldBeNull();
        first.Items.ShouldAllBe(item => item.SizeBytes == 3 && item.ContentType == null);
    }

    [Fact]
    public async Task MissingReadAndMetadata_ReturnNull_AndDeleteIsIdempotent()
    {
        var key = $"missing/{Guid.NewGuid():N}.bin";
        using var provider = CreateProvider();

        (await provider.ReadAsync(key, TestContext.Current.CancellationToken)).ShouldBeNull();
        (await provider.GetMetadataAsync(key, TestContext.Current.CancellationToken)).ShouldBeNull();
        await provider.DeleteAsync(key, TestContext.Current.CancellationToken);
        await provider.DeleteAsync(key, TestContext.Current.CancellationToken);

        (await provider.ExistsAsync(key, TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Operations_HonorAlreadyCanceledToken()
    {
        using var provider = CreateProvider();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => provider.ListAsync(new ListStorageObjectsRequest(""), cts.Token));
    }
}

public sealed class S3StorageProviderDisposalTests
{
    private static IOptions<S3StorageOptions> ValidOptions() => Options.Create(new S3StorageOptions
    {
        BucketName = "media",
        Region = "us-east-1"
    });

    [Fact]
    public void Dispose_InjectedClient_DoesNotDisposeClient()
    {
        var client = Substitute.For<IAmazonS3>();
        var provider = new S3StorageProvider(client, ValidOptions());

        provider.Dispose();

        ((IDisposable)client).DidNotReceive().Dispose();
    }

    [Fact]
    public void Dispose_OwnedClient_DisposesClient()
    {
        var client = Substitute.For<IAmazonS3>();
        var provider = new S3StorageProvider(client, ValidOptions(), ownsClient: true);

        provider.Dispose();

        ((IDisposable)client).Received(1).Dispose();
    }

    [Fact]
    public async Task DisposeReadResult_DisposesAwsResponseStream()
    {
        var stream = new TrackingStream();
        var response = new GetObjectResponse
        {
            ResponseStream = stream,
            ContentLength = 3,
            Headers = { ContentType = "application/octet-stream" }
        };
        var client = Substitute.For<IAmazonS3>();
        client.GetObjectAsync("media", "key.bin", Arg.Any<CancellationToken>()).Returns(response);
        using var provider = new S3StorageProvider(client, ValidOptions());

        var result = await provider.ReadAsync("key.bin", TestContext.Current.CancellationToken);
        await result!.DisposeAsync();

        stream.WasDisposed.ShouldBeTrue();
    }

    private sealed class TrackingStream : MemoryStream
    {
        public bool WasDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
