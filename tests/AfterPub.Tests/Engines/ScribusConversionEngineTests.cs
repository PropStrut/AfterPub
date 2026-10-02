using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;

namespace AfterPub.Tests.Engines;

public sealed class ScribusConversionEngineTests : IDisposable
{
    private readonly string _tempRoot;

    public ScribusConversionEngineTests()
    {
        this._tempRoot = Path.Combine(Path.GetTempPath(), "AfterPubTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this._tempRoot);
    }

    public void Dispose()
    {
        Directory.Delete(this._tempRoot, true);
    }

    [Fact]
    public void IsAvailable_NothingInstalled_ReturnsFalse()
    {
        ScribusConversionEngine engine = this.CreateEngineWithNoInstall();

        Assert.False(engine.IsAvailable());
    }

    [Fact]
    public void ConvertToPdf_NothingInstalled_FailsWithClearMessage()
    {
        ScribusConversionEngine engine = this.CreateEngineWithNoInstall();
        string sourcePath = Path.Combine(this._tempRoot, "flyer.pub");
        SourceFile source = new SourceFile(sourcePath, "flyer.pub", 0, DateTime.UtcNow);

        ConversionOutcome outcome = engine.ConvertToPdf(source, Path.Combine(this._tempRoot, "flyer.pdf"));

        Assert.False(outcome.Success);
        Assert.Contains("Scribus was not found", outcome.ErrorMessage);
    }

    [Fact]
    public void Kind_IsScribus()
    {
        ScribusConversionEngine engine = this.CreateEngineWithNoInstall();

        Assert.Equal(EngineKind.Scribus, engine.Kind);
    }

    private ScribusConversionEngine CreateEngineWithNoInstall()
    {
        string emptyRoot = Path.Combine(this._tempRoot, "programfiles");
        Directory.CreateDirectory(emptyRoot);
        return new ScribusConversionEngine(null, new ScribusLocator(new[] { emptyRoot }));
    }
}
