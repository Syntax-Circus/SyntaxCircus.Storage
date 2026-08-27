namespace SyntaxCircus.Storage.Tests;

public sealed class S3StorageOptionsTests
{
    [Fact]
    public void Defaults_AreSafeForAws()
    {
        var options = new S3StorageOptions();

        options.BucketName.ShouldBe(string.Empty);
        options.Region.ShouldBe("us-east-1");
        options.ServiceUrl.ShouldBeNull();
        options.AccessKey.ShouldBeNull();
        options.SecretKey.ShouldBeNull();
        options.ForcePathStyle.ShouldBeFalse();
    }

    [Fact]
    public void SectionName_IsStorageS3()
    {
        S3StorageOptions.SectionName.ShouldBe("Storage:S3");
    }

    [Theory]
    [InlineData(null, "secret")]
    [InlineData("access", null)]
    public void Validate_CredentialsMustBeConfiguredTogether(string? accessKey, string? secretKey)
    {
        var options = ValidOptions();
        options.AccessKey = accessKey;
        options.SecretKey = secretKey;

        var result = S3StorageOptions.Validate(options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AccessKey");
        result.FailureMessage.ShouldContain("SecretKey");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BucketNameIsRequired(string bucketName)
    {
        var options = ValidOptions();
        options.BucketName = bucketName;

        S3StorageOptions.Validate(options).Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_RegionIsRequired(string region)
    {
        var options = ValidOptions();
        options.Region = region;

        S3StorageOptions.Validate(options).Failed.ShouldBeTrue();
    }

    [Theory]
    [InlineData("localhost:9000")]
    [InlineData("ftp://localhost/storage")]
    public void Validate_ServiceUrlMustBeAbsoluteHttpEndpoint(string serviceUrl)
    {
        var options = ValidOptions();
        options.ServiceUrl = serviceUrl;

        var result = S3StorageOptions.Validate(options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("ServiceUrl");
    }

    private static S3StorageOptions ValidOptions() => new()
    {
        BucketName = "media",
        Region = "us-east-1"
    };
}
