namespace SyntaxCircus.Storage.Tests;

public class StorageServiceCollectionExtensionsTests
{
    private static IConfiguration BuildConfiguration(string? provider = null)
    {
        var dict = new Dictionary<string, string?>();
        if (provider is not null)
        {
            dict["Storage:Provider"] = provider;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public void AddStorageProvider_NullServices_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            StorageServiceCollectionExtensions.AddStorageProvider(null!, BuildConfiguration()));
    }

    [Fact]
    public void AddStorageProvider_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddStorageProvider(null!));
    }

    [Fact]
    public void AddStorageProvider_NoProviderConfigured_RegistersLocalFileStorageProvider()
    {
        var services = new ServiceCollection();
        services.AddStorageProvider(BuildConfiguration());

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IStorageProvider>().ShouldBeOfType<LocalFileStorageProvider>();
    }

    [Theory]
    [InlineData("Local")]
    [InlineData("local")]
    [InlineData("LOCAL")]
    public void AddStorageProvider_LocalProviderCaseInsensitive_RegistersLocalFileStorageProvider(string provider)
    {
        var services = new ServiceCollection();
        services.AddStorageProvider(BuildConfiguration(provider));

        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<IStorageProvider>().ShouldBeOfType<LocalFileStorageProvider>();
    }

    [Fact]
    public void AddStorageProvider_UnknownProvider_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var ex = Should.Throw<InvalidOperationException>(() => services.AddStorageProvider(BuildConfiguration("S3")));
        ex.Message.ShouldContain("S3");
    }

    [Fact]
    public void AddStorageProvider_BindsLocalStorageOptions()
    {
        var dict = new Dictionary<string, string?> { ["Storage:Local:RootPath"] = "/data/blobs" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(dict).Build();

        var services = new ServiceCollection();
        services.AddStorageProvider(configuration);

        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<IOptions<LocalStorageOptions>>().Value.RootPath.ShouldBe("/data/blobs");
    }
}
