using AfterPub.Core.Conversion;
using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;
using Xunit;

namespace AfterPub.Tests.Conversion;

public class RowConverterScratchPdfTests : IDisposable
{
    private readonly string _tempRoot;

    public RowConverterScratchPdfTests()
    {
        this._tempRoot = Directory.CreateTempSubdirectory("AfterPubRowTests_").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempRoot))
        {
            Directory.Delete(this._tempRoot, recursive: true);
        }
    }

    // Writes whatever PDF path it is given, and remembers that path.
    private sealed class RecordingPdfEngine : IConversionEngine
    {
        public string? LastPdfPath { get; private set; }

        public EngineKind Kind => EngineKind.LibreOffice;

        public bool IsAvailable()
        {
            return true;
        }

        public ConversionOutcome ConvertToPdf(SourceFile source, string outputPdfPath)
        {
            this.LastPdfPath = outputPdfPath;
            Directory.CreateDirectory(Path.GetDirectoryName(outputPdfPath)!);
            File.WriteAllText(outputPdfPath, "ENGINE PDF");
            return ConversionOutcome.Ok();
        }
    }

    private sealed class WritingOdgConverter : IPdfToOdgConverter
    {
        public bool IsAvailable()
        {
            return true;
        }

        public ConversionOutcome ConvertPdfToOdg(string sourcePdfPath, string outputOdgPath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputOdgPath)!);
            File.WriteAllText(outputOdgPath, "ODG");
            return ConversionOutcome.Ok();
        }
    }

    private ConversionRow ScanFlyer()
    {
        PubFileScanner scanner = new PubFileScanner(new OutputPathResolver());
        ScanOptions options = new ScanOptions { RootFolder = this._tempRoot, Recursive = false };
        return Assert.Single(scanner.Scan(options));
    }

    [Fact]
    public void Convert_OdgOnly_NeverTouchesAnExistingPdf()
    {
        File.WriteAllText(Path.Combine(this._tempRoot, "Flyer.pub"), "placeholder");
        string realPdf = Path.Combine(this._tempRoot, "Flyer.pdf");
        File.WriteAllText(realPdf, "REAL PDF");
        RecordingPdfEngine engine = new RecordingPdfEngine();
        RowConverter converter = new RowConverter(engine, new WritingOdgConverter());

        RowConversionResult result = converter.Convert(this.ScanFlyer(), new[] { OutputTarget.Odg });

        Assert.Equal("REAL PDF", File.ReadAllText(realPdf));
        Assert.NotEqual(realPdf, engine.LastPdfPath);
        Assert.True(result.ForTarget(OutputTarget.Odg)!.Success);
        Assert.True(File.Exists(Path.Combine(this._tempRoot, "Flyer.odg")));
    }

    [Fact]
    public void Convert_OdgOnly_RemovesTheScratchPdfAndItsFolder()
    {
        File.WriteAllText(Path.Combine(this._tempRoot, "Flyer.pub"), "placeholder");
        RecordingPdfEngine engine = new RecordingPdfEngine();
        RowConverter converter = new RowConverter(engine, new WritingOdgConverter());

        converter.Convert(this.ScanFlyer(), new[] { OutputTarget.Odg });

        Assert.NotNull(engine.LastPdfPath);
        Assert.False(File.Exists(engine.LastPdfPath));
        Assert.False(Directory.Exists(Path.GetDirectoryName(engine.LastPdfPath)));
        Assert.False(File.Exists(Path.Combine(this._tempRoot, "Flyer.pdf")));
    }

    [Fact]
    public void Convert_PdfAndOdg_UsesTheRealPdfPath()
    {
        File.WriteAllText(Path.Combine(this._tempRoot, "Flyer.pub"), "placeholder");
        string realPdf = Path.Combine(this._tempRoot, "Flyer.pdf");
        RecordingPdfEngine engine = new RecordingPdfEngine();
        RowConverter converter = new RowConverter(engine, new WritingOdgConverter());

        converter.Convert(this.ScanFlyer(), new[] { OutputTarget.Pdf, OutputTarget.Odg });

        Assert.Equal(realPdf, engine.LastPdfPath);
        Assert.True(File.Exists(realPdf));
    }
}
