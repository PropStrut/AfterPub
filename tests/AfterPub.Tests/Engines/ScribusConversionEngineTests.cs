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
        SourceFile source = this.CreateSource();

        ConversionOutcome outcome = engine.ConvertToPdf(source, Path.Combine(this._tempRoot, "flyer.pdf"));

        Assert.False(outcome.Success);
        Assert.Contains("Scribus was not found", outcome.ErrorMessage);
    }

    [Fact]
    public void ConvertToSla_NothingInstalled_FailsWithClearMessage()
    {
        ScribusConversionEngine engine = this.CreateEngineWithNoInstall();
        SourceFile source = this.CreateSource();

        ConversionOutcome outcome = engine.ConvertToSla(source, Path.Combine(this._tempRoot, "flyer.sla"));

        Assert.False(outcome.Success);
        Assert.Contains("Scribus was not found", outcome.ErrorMessage);
    }

    [Fact]
    public void Kind_IsScribus()
    {
        ScribusConversionEngine engine = this.CreateEngineWithNoInstall();

        Assert.Equal(EngineKind.Scribus, engine.Kind);
    }

    private SourceFile CreateSource()
    {
        string sourcePath = Path.Combine(this._tempRoot, "flyer.pub");
        return new SourceFile(sourcePath, "flyer.pub", 0, DateTime.UtcNow);
    }

    private ScribusConversionEngine CreateEngineWithNoInstall()
    {
        string emptyRoot = Path.Combine(this._tempRoot, "programfiles");
        Directory.CreateDirectory(emptyRoot);
        return new ScribusConversionEngine(null, new ScribusLocator(new[] { emptyRoot }));
    }
}
