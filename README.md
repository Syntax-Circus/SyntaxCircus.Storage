# SyntaxCircus.Storage

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.Storage/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.Storage/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.Storage.svg)](https://www.nuget.org/packages/SyntaxCircus.Storage)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

A stream-based file/blob storage abstraction with local-disk and S3-compatible implementations and a config-driven provider switch. No ASP.NET Core dependency — usable from any .NET host.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## Usage

```csharp
builder.Services.AddStorageProvider(builder.Configuration);
```

```json
{
  "Storage": {
    "Provider": "Local",
    "Local": { "RootPath": "/var/data/my-app" }
  }
}
```

```csharp
public sealed class WidgetService(IStorageProvider storage)
{
    public async Task<StoredObject> UploadAsync(string id, Stream content, CancellationToken ct)
        => await storage.StoreAsync(new StoreObjectRequest($"widgets/{id}.bin", content), ct);
}
```

`IStorageProvider` — `StoreAsync`/`ReadAsync`/`ExistsAsync`/`DeleteAsync`, all keyed by an opaque string path-like key. `ReadAsync` returns `StorageReadResult?` (`null` if the key doesn't exist), which is itself an `IAsyncDisposable` wrapping the content stream — dispose it (or `await using`) once you're done reading.

Providers can also implement `GetMetadataAsync` and `ListAsync`. Metadata reads return size, content type when the provider preserves it, and the UTC last-modified instant. Listing is prefix-scoped, ordinally ordered, and bounded to 1–1,000 objects per page; pass the prior page's `NextAfterKey` back as `AfterKey` to continue. These are default interface methods so existing third-party providers keep compiling and receive `NotSupportedException` until they opt in.

`AddStorageProvider` recognizes `"Local"` (the default) and `"S3"` case-insensitively. Local storage writes under a configured `RootPath`, with path-traversal and rooted/absolute keys rejected.

### S3-compatible storage

For AWS S3, the bucket and region are required. When access and secret keys are omitted, the AWS SDK's normal credential chain is used.

```json
{
  "Storage": {
    "Provider": "S3",
    "S3": {
      "BucketName": "my-app-media",
      "Region": "us-east-1"
    }
  }
}
```

For MinIO or another S3-compatible endpoint, set `ServiceUrl`, provide both credentials, and normally enable path-style addressing:

```json
{
  "Storage": {
    "Provider": "S3",
    "S3": {
      "BucketName": "my-app-media",
      "Region": "us-east-1",
      "ServiceUrl": "http://minio:9000",
      "AccessKey": "set-with-secret-configuration",
      "SecretKey": "set-with-secret-configuration",
      "ForcePathStyle": true
    }
  }
}
```

Do not commit production credentials to configuration files. `BucketName` and `Region` must be non-empty, and `AccessKey` and `SecretKey` must be configured together. `S3StorageProvider(IOptions<S3StorageOptions>)` creates and owns its AWS client; the overload accepting `IAmazonS3` leaves the injected client owned by the caller.

S3 listings preserve the shared ordinal, prefix/`AfterKey`, bounded-page contract. List responses do not include object content type, so listed metadata reports a null content type; call `GetMetadataAsync` when content type is required. An S3 `StorageReadResult` owns the underlying AWS response and must be disposed.

## Building access URLs

```json
{
  "Storage": {
    "Provider": "Local",
    "Local": {
      "RootPath": "/var/data/my-app",
      "PublicBaseUrl": "https://cdn.example.com/files"
    }
  }
}
```

```csharp
var url = await storage.GetAccessUrlAsync("widgets/abc.bin", ct); // https://cdn.example.com/files/widgets/abc.bin
```

`GetAccessUrlAsync` is a default interface method, so it's additive — any existing `IStorageProvider` implementation keeps compiling unchanged and simply doesn't support it (throws `NotSupportedException`) until it opts in. `LocalFileStorageProvider` builds a stable `{PublicBaseUrl}/{key}` URL and requires `PublicBaseUrl` to be configured; the `expiry` parameter exists for providers that support signed/time-limited URLs and is ignored on local disk.

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
