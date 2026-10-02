using AfterPub.Core.Scanning;
using AfterPub.Core.Settings;
using Xunit;

namespace AfterPub.Tests.Settings;

public class AppSettingsStoreTests : IDisposable
{
    private readonly string _tempRoot;

    public AppSettingsStoreTests()
    {
        this._tempRoot = Directory.CreateTempSubdirectory("AfterPubSettingsTests_").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempRoot))
        {
            Directory.Delete(this._tempRoot, recursive: true);
        }
    }

    private string SettingsFilePath()
    {
        return Path.Combine(this._tempRoot, "afterpub.settings.json");
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileDoesNotExist()
    {
        AppSettingsStore store = new AppSettingsStore(this.SettingsFilePath());

        AppSettings settings = store.Load();

        Assert.False(settings.Recursive);
        Assert.Equal(OutputLocationMode.SameFolder, settings.LocationMode);
        Assert.Null(settings.SeparateOutputRoot);
    }

    [Fact]
    public void Load_ReturnsDefaults_WhenFileIsCorrupt()
    {
        string path = this.SettingsFilePath();
        File.WriteAllText(path, "{ this is not valid json");
        AppSettingsStore store = new AppSettingsStore(path);

        AppSettings settings = store.Load();

        Assert.False(settings.Recursive);
        Assert.Equal(OutputLocationMode.SameFolder, settings.LocationMode);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        AppSettingsStore store = new AppSettingsStore(this.SettingsFilePath());
        AppSettings original = new AppSettings
        {
            Recursive = true,
            LocationMode = OutputLocationMode.SeparateFolder,
            SeparateOutputRoot = @"D:\AfterPubOutput"
        };

        store.Save(original);
        AppSettings loaded = store.Load();

        Assert.True(loaded.Recursive);
        Assert.Equal(OutputLocationMode.SeparateFolder, loaded.LocationMode);
        Assert.Equal(@"D:\AfterPubOutput", loaded.SeparateOutputRoot);
    }

    [Fact]
    public void Save_CreatesParentFolder_WhenItDoesNotExist()
    {
        string nestedPath = Path.Combine(this._tempRoot, "NewSubfolder", "afterpub.settings.json");
        AppSettingsStore store = new AppSettingsStore(nestedPath);

        store.Save(new AppSettings());

        Assert.True(File.Exists(nestedPath));
    }

    [Fact]
    public void Save_WritesEnumsAsReadableStrings()
    {
        string path = this.SettingsFilePath();
        AppSettingsStore store = new AppSettingsStore(path);
        AppSettings settings = new AppSettings { LocationMode = OutputLocationMode.SeparateFolder };

        store.Save(settings);
        string json = File.ReadAllText(path);

        Assert.Contains("SeparateFolder", json);
    }
}
