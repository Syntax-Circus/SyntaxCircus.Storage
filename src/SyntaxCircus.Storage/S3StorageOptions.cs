namespace SyntaxCircus.Storage;

public sealed class S3StorageOptions
{
    public const string SectionName = "Storage:S3";

    public string BucketName { get; set; } = string.Empty;

    public string Region { get; set; } = "us-east-1";

    public string? ServiceUrl { get; set; }

    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    public bool ForcePathStyle { get; set; }

    public static ValidateOptionsResult Validate(S3StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.BucketName))
        {
            failures.Add($"{nameof(BucketName)} is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Region))
        {
            failures.Add($"{nameof(Region)} is required.");
        }

        if (!string.IsNullOrWhiteSpace(options.ServiceUrl) &&
            (!Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out var serviceUri) ||
             (serviceUri.Scheme != Uri.UriSchemeHttp && serviceUri.Scheme != Uri.UriSchemeHttps)))
        {
            failures.Add($"{nameof(ServiceUrl)} must be an absolute HTTP or HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKey) != string.IsNullOrWhiteSpace(options.SecretKey))
        {
            failures.Add($"{nameof(AccessKey)} and {nameof(SecretKey)} must be configured together.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
