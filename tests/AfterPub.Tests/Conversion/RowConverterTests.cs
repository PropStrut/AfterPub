using AfterPub.Core.Conversion;
using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;
using Xunit;

namespace AfterPub.Tests.Conversion;

/// <summary>Fake .pub -&gt; PDF engine. Writes a dummy file on success so cleanup
/// behavior (scratch PDFs get deleted, kept PDFs do not) can be asserted for real.</summary>
internal sealed class FakeConversionEngine : IConversionEngine
{
    public EngineKind Kind => EngineKind.Publisher;
    public bool Available { get; set; } = true;
    public ConversionOutcome ResultToReturn { get; set; } = ConversionOutcome.Ok();
    public List<(SourceFile Source, string OutputPath)> Calls { get; } = new List<(SourceFile, string)>();

    public bool IsAvailable()
    {
        return this.Available;
    }

    public ConversionOutcome ConvertToPdf(SourceFile source, string outputPdfPath)
    {
        this.Calls.Add((source, outputPdfPath));
        if (this.ResultToReturn.Success)
        {
            File.WriteAllText(outputPdfPath, "dummy pdf");
        }

        return this.ResultToReturn;
    }
}

internal sealed class FakePdfToOdgConverter : IPdfToOdgConverter
{
    public bool Available { get; set; } = true;
    public ConversionOutcome ResultToReturn { get; set; } = ConversionOutcome.Ok();
    public List<(string PdfPath, string OutputPath)> Calls { get; } = new List<(string, string)>();

    public bool IsAvailable()
    {
        return this.Available;
    }

    public ConversionOutcome ConvertPdfToOdg(string sourcePdfPath, string outputOdgPath)
    {
        this.Calls.Add((sourcePdfPath, outputOdgPath));
        return this.ResultToReturn;
    }
}

public class RowConverterTests : IDisposable
{
    private readonly string _tempRoot;

    public RowConverterTests()
    {
        this._tempRoot = Directory.CreateTempSubdirectory("AfterPubRowConverterTests_").FullName;
    }

    public void Dispose()
    {
        if (Directory.Exists(this._tempRoot))
        {
            Directory.Delete(this._tempRoot, recursive: true);
        }
    }

    private ConversionRow MakeRow(string baseName, IReadOnlyList<OutputInfo> outputs)
    {
        string sourcePath = Path.Combine(this._tempRoot, baseName + ".pub");
        SourceFile source = new SourceFile(sourcePath, baseName + ".pub", sizeBytes: 100, lastWriteTimeUtc: DateTime.UtcNow);
        return new ConversionRow(source, outputs);
    }

    private OutputInfo MakeOutput(OutputTarget target, string fileName)
    {
        return OutputInfo.FromFileSystem(target, Path.Combine(this._tempRoot, fileName));
    }

    [Fact]
    public void Convert_PdfOnly_UsesRealResolvedPath_NoOdgCall()
    {
        ConversionRow row = this.MakeRow("Flyer", new[] { this.MakeOutput(OutputTarget.Pdf, "Flyer.pdf") });
        FakeConversionEngine pubEngine = new FakeConversionEngine();
        FakePdfToOdgConverter odgConverter = new FakePdfToOdgConverter();
        RowConverter converter = new RowConverter(pubEngine, odgConverter);

        RowConversionResult result = converter.Convert(row, new[] { OutputTarget.Pdf });

        Assert.True(result.ForTarget(OutputTarget.Pdf)!.Success);
        Assert.Null(result.ForTarget(OutputTarget.Odg));
        Assert.Single(pubEngine.Calls);
        Assert.Equal(Path.Combine(this._tempRoot, "Flyer.pdf"), pubEngine.Calls[0].OutputPath);
        Assert.Empty(odgConverter.Calls);
    }

    [Fact]
    public void Convert_OdgOnly_UsesScratchPdfPath_AndDeletesItAfterward()
    {
        ConversionRow row = this.MakeRow("Flyer", new[] { this.MakeOutput(OutputTarget.Odg, "Flyer.odg") });
        FakeConversionEngine pubEngine = new FakeConversionEngine();
        FakePdfToOdgConverter odgConverter = new FakePdfToOdgConverter();
        RowConverter converter = new RowConverter(pubEngine, odgConverter);

        RowConversionResult result = converter.Convert(row, new[] { OutputTarget.Odg });

        Assert.Null(result.ForTarget(OutputTarget.Pdf));
        Assert.True(result.ForTarget(OutputTarget.Odg)!.Success);

        string scratchPath = pubEngine.Calls[0].OutputPath;
        Assert.DoesNotContain(this._tempRoot, scratchPath); // not the real output folder
        Assert.EndsWith("Flyer.pdf", scratchPath);
        Assert.Equal(scratchPath, odgConverter.Calls[0].PdfPath);
        Assert.Equal(Path.Combine(this._tempRoot, "Flyer.odg"), odgConverter.Calls[0].OutputPath);
        Assert.False(File.Exists(scratchPath));
    }

    [Fact]
    public void Convert_Both_KeepsPdfAtRealPath_AndDoesNotDeleteIt()
    {
        ConversionRow row = this.MakeRow(
            "Flyer",
            new[]
            {
                this.MakeOutput(OutputTarget.Pdf, "Flyer.pdf"),
                this.MakeOutput(OutputTarget.Odg, "Flyer.odg")
            });
        FakeConversionEngine pubEngine = new FakeConversionEngine();
        FakePdfToOdgConverter odgConverter = new FakePdfToOdgConverter();
        RowConverter converter = new RowConverter(pubEngine, odgConverter);

        RowConversionResult result = converter.Convert(row, new[] { OutputTarget.Pdf, OutputTarget.Odg });

        Assert.True(result.ForTarget(OutputTarget.Pdf)!.Success);
        Assert.True(result.ForTarget(OutputTarget.Odg)!.Success);

        string realPdfPath = Path.Combine(this._tempRoot, "Flyer.pdf");
        Assert.Equal(realPdfPath, pubEngine.Calls[0].OutputPath);
        Assert.Equal(realPdfPath, odgConverter.Calls[0].PdfPath);
        Assert.True(File.Exists(realPdfPath)); // kept, not scratch
    }

    [Fact]
    public void Convert_OdgReflectsPdfFailure_AndNeverCallsOdgConverter()
    {
        ConversionRow row = this.MakeRow(
            "Flyer",
            new[]
            {
                this.MakeOutput(OutputTarget.Pdf, "Flyer.pdf"),
                this.MakeOutput(OutputTarget.Odg, "Flyer.odg")
            });
        FakeConversionEngine pubEngine = new FakeConversionEngine { ResultToReturn = ConversionOutcome.Failed("boom") };
        FakePdfToOdgConverter odgConverter = new FakePdfToOdgConverter();
        RowConverter converter = new RowConverter(pubEngine, odgConverter);

        RowConversionResult result = converter.Convert(row, new[] { OutputTarget.Pdf, OutputTarget.Odg });

        Assert.False(result.ForTarget(OutputTarget.Pdf)!.Success);
        Assert.False(result.ForTarget(OutputTarget.Odg)!.Success);
        Assert.Contains("boom", result.ForTarget(OutputTarget.Odg)!.ErrorMessage);
        Assert.Empty(odgConverter.Calls);
    }

    [Fact]
    public void Convert_OdgFails_WhenLibreOfficeUnavailable()
    {
        ConversionRow row = this.MakeRow("Flyer", new[] { this.MakeOutput(OutputTarget.Odg, "Flyer.odg") });
        FakeConversionEngine pubEngine = new FakeConversionEngine();
        FakePdfToOdgConverter odgConverter = new FakePdfToOdgConverter { Available = false };
        RowConverter converter = new RowConverter(pubEngine, odgConverter);

        RowConversionResult result = converter.Convert(row, new[] { OutputTarget.Odg });

        Assert.False(result.ForTarget(OutputTarget.Odg)!.Success);
        Assert.Empty(odgConverter.Calls);
    }

    [Fact]
    public void Convert_PdfWantedButNotResolved_FailsWithoutCallingEngine()
    {
        ConversionRow row = this.MakeRow("Flyer", Array.Empty<OutputInfo>());
        FakeConversionEngine pubEngine = new FakeConversionEngine();
        FakePdfToOdgConverter odgConverter = new FakePdfToOdgConverter();
        RowConverter converter = new RowConverter(pubEngine, odgConverter);

        RowConversionResult result = converter.Convert(row, new[] { OutputTarget.Pdf });

        Assert.False(result.ForTarget(OutputTarget.Pdf)!.Success);
        Assert.Empty(pubEngine.Calls);
    }

    [Fact]
    public void Convert_NoTargetsWanted_ReturnsEmptyResult()
    {
        ConversionRow row = this.MakeRow("Flyer", Array.Empty<OutputInfo>());
        FakeConversionEngine pubEngine = new FakeConversionEngine();
        FakePdfToOdgConverter odgConverter = new FakePdfToOdgConverter();
        RowConverter converter = new RowConverter(pubEngine, odgConverter);

        RowConversionResult result = converter.Convert(row, Array.Empty<OutputTarget>());

        Assert.Empty(result.OutcomesByTarget);
        Assert.Empty(pubEngine.Calls);
        Assert.Empty(odgConverter.Calls);
    }
}
