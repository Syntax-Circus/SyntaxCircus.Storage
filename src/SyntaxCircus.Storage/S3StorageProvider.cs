using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace SyntaxCircus.Storage;

public sealed class S3StorageProvider : IStorageProvider, IDisposable
{
    private readonly IAmazonS3 client;
    private readonly S3StorageOptions options;
    private readonly bool ownsClient;
    private int disposed;

    public S3StorageProvider(IOptions<S3StorageOptions> options)
    {
        this.options = GetValidatedOptions(options);
        client = CreateClient(this.options);
        ownsClient = true;
    }

    public S3StorageProvider(IAmazonS3 client, IOptions<S3StorageOptions> options)
        : this(client, options, ownsClient: false)
    {
    }

    internal S3StorageProvider(IAmazonS3 client, IOptions<S3StorageOptions> options, bool ownsClient)
    {
        ArgumentNullException.ThrowIfNull(client);

        this.options = GetValidatedOptions(options);
        this.client = client;
        this.ownsClient = ownsClient;
    }

    public async Task<StoredObject> StoreAsync(StoreObjectRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateKey(request.Key);
        cancellationToken.ThrowIfCancellationRequested();

        var putRequest = new PutObjectRequest
        {
            BucketName = options.BucketName,
            Key = request.Key,
            InputStream = request.Content,
            ContentType = request.ContentType,
            AutoCloseStream = false
        };

        await client.PutObjectAsync(putRequest, cancellationToken).ConfigureAwait(false);

        var metadata = await GetMetadataAsync(request.Key, cancellationToken).ConfigureAwait(false);
        return new StoredObject(request.Key, metadata!.SizeBytes);
    }

    public async Task<StorageReadResult?> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        GetObjectResponse response;
        try
        {
            response = await client.GetObjectAsync(options.BucketName, key, cancellationToken).ConfigureAwait(false);
        }
        catch (AmazonS3Exception exception) when (IsMissing(exception))
        {
            return null;
        }

        var content = new ResponseOwnedStream(response.ResponseStream, response);
        return new StorageReadResult(content, response.Headers.ContentType, response.ContentLength);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        => await GetMetadataAsync(key, cancellationToken).ConfigureAwait(false) is not null;

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        await client.DeleteObjectAsync(options.BucketName, key, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StorageObjectMetadata?> GetMetadataAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        ValidateKey(key);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var response = await client
                .GetObjectMetadataAsync(options.BucketName, key, cancellationToken)
                .ConfigureAwait(false);
            return new StorageObjectMetadata(
                key,
                response.ContentLength,
                response.Headers.ContentType,
                new DateTimeOffset(response.LastModified ?? DateTime.UnixEpoch, TimeSpan.Zero));
        }
        catch (AmazonS3Exception exception) when (IsMissing(exception))
        {
            return null;
        }
    }

    public async Task<StorageObjectPage> ListAsync(
        ListStorageObjectsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var listRequest = new ListObjectsV2Request
        {
            BucketName = options.BucketName,
            Prefix = request.Prefix,
            StartAfter = request.AfterKey,
            MaxKeys = request.PageSize + 1
        };

        var response = await client.ListObjectsV2Async(listRequest, cancellationToken).ConfigureAwait(false);
        var objects = (response.S3Objects ?? [])
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToArray();
        var hasMore = objects.Length > request.PageSize || response.IsTruncated == true;
        var pageObjects = objects.Take(request.PageSize).ToArray();
        var items = pageObjects
            .Select(item => new StorageObjectMetadata(
                item.Key,
                item.Size ?? 0,
                ContentType: null,
                new DateTimeOffset(item.LastModified ?? DateTime.UnixEpoch, TimeSpan.Zero)))
            .ToArray();
        var nextAfterKey = hasMore && items.Length > 0 ? items[^1].Key : null;
        return new StorageObjectPage(items, nextAfterKey);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0 && ownsClient)
        {
            client.Dispose();
        }
    }

    private static S3StorageOptions GetValidatedOptions(IOptions<S3StorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var value = options.Value;
        var validation = S3StorageOptions.Validate(value);
        if (validation.Failed)
        {
            throw new OptionsValidationException(
                S3StorageOptions.SectionName,
                typeof(S3StorageOptions),
                validation.Failures);
        }

        return value;
    }

    private static AmazonS3Client CreateClient(S3StorageOptions options)
    {
        var config = new AmazonS3Config
        {
            ForcePathStyle = options.ForcePathStyle
        };
        if (string.IsNullOrWhiteSpace(options.ServiceUrl))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(options.Region);
        }
        else
        {
            config.ServiceURL = options.ServiceUrl;
            config.AuthenticationRegion = options.Region;
        }

        return string.IsNullOrWhiteSpace(options.AccessKey)
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
    }

    private static bool IsMissing(AmazonS3Exception exception) =>
        exception.StatusCode == HttpStatusCode.NotFound ||
        string.Equals(exception.ErrorCode, "NoSuchKey", StringComparison.Ordinal) ||
        string.Equals(exception.ErrorCode, "NotFound", StringComparison.Ordinal);

    private static void ValidateKey(string key) => ArgumentException.ThrowIfNullOrWhiteSpace(key);

    private sealed class ResponseOwnedStream(Stream content, IDisposable response) : Stream
    {
        private int disposed;

        public override bool CanRead => content.CanRead;
        public override bool CanSeek => content.CanSeek;
        public override bool CanWrite => content.CanWrite;
        public override long Length => content.Length;
        public override long Position { get => content.Position; set => content.Position = value; }

        public override void Flush() => content.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => content.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => content.Read(buffer, offset, count);
        public override int Read(Span<byte> buffer) => content.Read(buffer);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            content.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => content.Seek(offset, origin);
        public override void SetLength(long value) => content.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => content.Write(buffer, offset, count);
        public override void Write(ReadOnlySpan<byte> buffer) => content.Write(buffer);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            content.WriteAsync(buffer, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing && Interlocked.Exchange(ref disposed, 1) == 0)
            {
                response.Dispose();
            }

            base.Dispose(disposing);
        }

    }
}
