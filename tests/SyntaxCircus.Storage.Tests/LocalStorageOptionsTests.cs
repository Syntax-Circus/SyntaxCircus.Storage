namespace SyntaxCircus.Storage.Tests;

public class LocalStorageOptionsTests
{
    [Fact]
    public void Defaults_AreEmpty()
    {
        var options = new LocalStorageOptions();

        options.RootPath.ShouldBe(string.Empty);
    }

    [Fact]
    public void SectionName_IsStorageLocal()
    {
        LocalStorageOptions.SectionName.ShouldBe("Storage:Local");
    }

    [Fact]
    public void RootPath_IsSettable()
    {
        var options = new LocalStorageOptions { RootPath = "/data" };

        options.RootPath.ShouldBe("/data");
    }
}
