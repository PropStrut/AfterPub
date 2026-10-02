using AfterPub.Core.Engines;

namespace AfterPub.Tests.Engines;

public sealed class ScribusLocatorTests : IDisposable
{
    private readonly string _tempRoot;

    public ScribusLocatorTests()
    {
        this._tempRoot = Path.Combine(Path.GetTempPath(), "AfterPubTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this._tempRoot);
    }

    public void Dispose()
    {
        Directory.Delete(this._tempRoot, true);
    }

    [Fact]
    public void Locate_PicksHighestVersionNumerically()
    {
        string root = this.CreateRoot("programfiles");
        this.CreateInstall(root, "Scribus 1.6.6");
        this.CreateInstall(root, "Scribus 1.10.0");
        this.CreateInstall(root, "Scribus 1.5.8");

        string? result = new ScribusLocator(new[] { root }).Locate(null);

        Assert.Equal(Path.Combine(root, "Scribus 1.10.0", "Scribus.exe"), result);
    }

    [Fact]
    public void Locate_SearchesAllRoots()
    {
        string rootA = this.CreateRoot("a");
        string rootB = this.CreateRoot("b");
        this.CreateInstall(rootA, "Scribus 1.5.8");
        this.CreateInstall(rootB, "Scribus 1.6.6");

        string? result = new ScribusLocator(new[] { rootA, rootB }).Locate(null);

        Assert.Equal(Path.Combine(rootB, "Scribus 1.6.6", "Scribus.exe"), result);
    }

    [Fact]
    public void Locate_ConfiguredPathThatExists_WinsOverSearch()
    {
        string root = this.CreateRoot("programfiles");
        this.CreateInstall(root, "Scribus 1.6.6");
        string custom = Path.Combine(this._tempRoot, "custom", "Scribus.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(custom)!);
        File.WriteAllText(custom, string.Empty);

        string? result = new ScribusLocator(new[] { root }).Locate(custom);

        Assert.Equal(custom, result);
    }

    [Fact]
    public void Locate_ConfiguredPathMissing_FallsBackToSearch()
    {
        string root = this.CreateRoot("programfiles");
        this.CreateInstall(root, "Scribus 1.6.6");
        string missing = Path.Combine(this._tempRoot, "nowhere", "Scribus.exe");

        string? result = new ScribusLocator(new[] { root }).Locate(missing);

        Assert.Equal(Path.Combine(root, "Scribus 1.6.6", "Scribus.exe"), result);
    }

    [Fact]
    public void Locate_NoInstall_ReturnsNull()
    {
        string root = this.CreateRoot("programfiles");

        string? result = new ScribusLocator(new[] { root }).Locate(null);

        Assert.Null(result);
    }

    [Fact]
    public void Locate_FolderWithoutExecutable_IsIgnored()
    {
        string root = this.CreateRoot("programfiles");
        Directory.CreateDirectory(Path.Combine(root, "Scribus 1.7.0"));
        this.CreateInstall(root, "Scribus 1.6.6");

        string? result = new ScribusLocator(new[] { root }).Locate(null);

        Assert.Equal(Path.Combine(root, "Scribus 1.6.6", "Scribus.exe"), result);
    }

    [Fact]
    public void Locate_MissingSearchRoot_DoesNotThrow()
    {
        string missingRoot = Path.Combine(this._tempRoot, "does-not-exist");

        string? result = new ScribusLocator(new[] { missingRoot }).Locate(null);

        Assert.Null(result);
    }

    private string CreateRoot(string name)
    {
        string root = Path.Combine(this._tempRoot, name);
        Directory.CreateDirectory(root);
        return root;
    }

    private void CreateInstall(string root, string folderName)
    {
        string folder = Path.Combine(root, folderName);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "Scribus.exe"), string.Empty);
    }
}
