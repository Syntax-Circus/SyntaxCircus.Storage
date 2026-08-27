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

    [Theory]
    [InlineData("S3")]
    [InlineData("s3")]
    public void AddStorageProvider_S3ProviderCaseInsensitive_RegistersS3StorageProvider(string provider)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:Provider"] = provider,
            ["Storage:S3:BucketName"] = "media",
            ["Storage:S3:Region"] = "us-east-1"
        }).Build();
        var services = new ServiceCollection();
        services.AddStorageProvider(configuration);

        using var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<IStorageProvider>().ShouldBeOfType<S3StorageProvider>();
    }

    [Fact]
    public void AddStorageProvider_UnknownProvider_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();

        var ex = Should.Throw<InvalidOperationException>(() => services.AddStorageProvider(BuildConfiguration("Azure")));
        ex.Message.ShouldContain("Azure");
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

    [Fact]
    public void AddStorageProvider_BindsS3StorageOptions()
    {
        var dict = new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "S3",
            ["Storage:S3:BucketName"] = "media",
            ["Storage:S3:Region"] = "us-west-2",
            ["Storage:S3:ServiceUrl"] = "http://localhost:9000",
            ["Storage:S3:AccessKey"] = "access",
            ["Storage:S3:SecretKey"] = "secret",
            ["Storage:S3:ForcePathStyle"] = "true"
        };
        var services = new ServiceCollection();
        services.AddStorageProvider(new ConfigurationBuilder().AddInMemoryCollection(dict).Build());

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<S3StorageOptions>>().Value;

        options.BucketName.ShouldBe("media");
        options.Region.ShouldBe("us-west-2");
        options.ServiceUrl.ShouldBe("http://localhost:9000");
        options.AccessKey.ShouldBe("access");
        options.SecretKey.ShouldBe("secret");
        options.ForcePathStyle.ShouldBeTrue();
    }
}
