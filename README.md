# SyntaxCircus.Storage

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.Storage/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.Storage/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

A stream-based file/blob storage abstraction, a local-disk implementation, and a config-driven provider switch. No ASP.NET Core dependency — usable from any .NET host.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## Usage

```csharp
builder.Services.AddStorageProvider(builder.Configuration); // binds "Storage" and "Storage:Local"
```

```json
{ "Storage": { "Provider": "Local" }, "Storage:Local": { "RootPath": "/var/data/my-app" } }
```

```csharp
public sealed class WidgetService(IStorageProvider storage)
{
    public async Task<StoredObject> UploadAsync(string id, Stream content, CancellationToken ct)
        => await storage.StoreAsync(new StoreObjectRequest($"widgets/{id}.bin", content), ct);
}
```

`IStorageProvider` — `StoreAsync`/`ReadAsync`/`ExistsAsync`/`DeleteAsync`, all keyed by an opaque string path-like key. `ReadAsync` returns `StorageReadResult?` (`null` if the key doesn't exist), which is itself an `IAsyncDisposable` wrapping the content stream — dispose it (or `await using`) once you're done reading.

Only `"Local"` is built in today (writes under a configured `RootPath`, with path-traversal keys rejected). Cloud-backed providers (S3, Azure Blob, etc.) aren't included yet — implement `IStorageProvider` yourself and register it directly instead of calling `AddStorageProvider`; the interface is designed so that isn't a breaking change to adopt later.

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
