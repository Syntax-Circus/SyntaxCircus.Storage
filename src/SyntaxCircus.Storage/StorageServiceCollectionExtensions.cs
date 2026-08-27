namespace SyntaxCircus.Storage;

public static class StorageServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IStorageProvider"/> from the <c>Storage:Provider</c> config value.
    /// <c>"Local"</c> is the default; <c>"S3"</c> enables the built-in S3-compatible provider.
    /// For other providers (Azure Blob, etc.),
    /// implement <see cref="IStorageProvider"/> yourself and register it with
    /// <c>services.AddSingleton&lt;IStorageProvider, YourProvider&gt;()</c> instead of calling this.
    /// </summary>
    public static IServiceCollection AddStorageProvider(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        services.Configure<LocalStorageOptions>(configuration.GetSection(LocalStorageOptions.SectionName));
        services.AddOptions<S3StorageOptions>()
            .Bind(configuration.GetSection(S3StorageOptions.SectionName))
            .Validate(options => S3StorageOptions.Validate(options).Succeeded, "Storage:S3 configuration is invalid.");

        var provider = configuration.GetSection(StorageOptions.SectionName)["Provider"];

        if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IStorageProvider, LocalFileStorageProvider>();
        }
        else if (string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IStorageProvider, S3StorageProvider>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Unknown storage provider '{provider}'. Use \"Local\" or \"S3\", or register your own IStorageProvider.");
        }

        return services;
    }
}
