namespace SyntaxCircus.Storage;

public sealed class LocalStorageOptions
{
    public const string SectionName = "Storage:Local";

    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Public base URL used by <see cref="LocalFileStorageProvider.GetAccessUrlAsync"/> to build a
    /// URL for a stored key (e.g. <c>https://cdn.example.com/files</c>). Only required if you call
    /// <c>GetAccessUrlAsync</c> — everything else works without it.
    /// </summary>
    public string? PublicBaseUrl { get; set; }
}
