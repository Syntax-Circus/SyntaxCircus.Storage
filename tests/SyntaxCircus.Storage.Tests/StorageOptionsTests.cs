namespace SyntaxCircus.Storage.Tests;

public class StorageOptionsTests
{
    [Fact]
    public void Defaults_ProviderIsLocal()
    {
        var options = new StorageOptions();

        options.Provider.ShouldBe("Local");
    }

    [Fact]
    public void SectionName_IsStorage()
    {
        StorageOptions.SectionName.ShouldBe("Storage");
    }
}
