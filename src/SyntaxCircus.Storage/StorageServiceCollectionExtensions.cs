namespace SyntaxCircus.Storage;

public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IStorageProvider"/> from the <c>Storage:Provider</c> config value.
    /// Only <c>"Local"</c> (the default) is built in — for other providers (S3, Azure Blob, etc.),
    /// implement <see cref="IStorageProvider"/> yourself and register it with
    /// <c>services.AddSingleton&lt;IStorageProvider, YourProvider&gt;()</c> instead of calling this.
    /// </summary>
    public static IServiceCollection AddStorageProvider(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<LocalStorageOptions>(configuration.GetSection(LocalStorageOptions.SectionName));

        var provider = configuration.GetSection(StorageOptions.SectionName)["Provider"];

        if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IStorageProvider, LocalFileStorageProvider>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown storage provider '{provider}'. Only \"Local\" is built in — register your own IStorageProvider for other providers.");
        }

        return services;
    }
}
