using AfterPub.Core.Conversion;
using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;

namespace AfterPub.Tests.Conversion;

public sealed class RowConverterSlaTests
{
    [Fact]
    public void Convert_SlaOnly_CallsSlaConverter_AndNeitherPdfNorOdgStage()
    {
        FakePdfEngine pdfEngine = new FakePdfEngine(ConversionOutcome.Ok());
        FakeOdgConverter odgConverter = new FakeOdgConverter();
        FakeSlaConverter slaConverter = new FakeSlaConverter(ConversionOutcome.Ok());
        RowConverter converter = new RowConverter(pdfEngine, odgConverter, slaConverter);
        ConversionRow row = CreateRow(@"C:\work\flyer.pub", (OutputTarget.Sla, @"C:\work\flyer.sla"));

        RowConversionResult result = converter.Convert(row, new[] { OutputTarget.Sla });

        ConversionOutcome? slaOutcome = result.ForTarget(OutputTarget.Sla);
        Assert.NotNull(slaOutcome);
        Assert.True(slaOutcome.Success);
        Assert.Equal(@"C:\work\flyer.sla", slaConverter.LastOutputPath);
        Assert.Equal(0, pdfEngine.CallCount);
        Assert.Equal(0, odgConverter.CallCount);
    }

    [Fact]
    public void Convert_SlaWithoutConverter_FailsThatTargetWithClearMessage()
    {
        FakePdfEngine pdfEngine = new FakePdfEngine(ConversionOutcome.Ok());
        RowConverter converter = new RowConverter(pdfEngine, new FakeOdgConverter());
        ConversionRow row = CreateRow(@"C:\work\flyer.pub", (OutputTarget.Sla, @"C:\work\flyer.sla"));

        RowConversionResult result = converter.Convert(row, new[] { OutputTarget.Sla });

        ConversionOutcome? slaOutcome = result.ForTarget(OutputTarget.Sla);
        Assert.NotNull(slaOutcome);
        Assert.False(slaOutcome.Success);
        Assert.Contains("Scribus is required", slaOutcome.ErrorMessage);
    }

    [Fact]
    public void Convert_PdfAndSla_SlaFailureDoesNotAffectPdf()
    {
        FakePdfEngine pdfEngine = new FakePdfEngine(ConversionOutcome.Ok());
        FakeSlaConverter slaConverter = new FakeSlaConverter(ConversionOutcome.Failed("boom"));
        RowConverter converter = new RowConverter(pdfEngine, new FakeOdgConverter(), slaConverter);
        ConversionRow row = CreateRow(
            @"C:\work\flyer.pub",
            (OutputTarget.Pdf, @"C:\work\flyer.pdf"),
            (OutputTarget.Sla, @"C:\work\flyer.sla"));

        RowConversionResult result = converter.Convert(row, new[] { OutputTarget.Pdf, OutputTarget.Sla });

        ConversionOutcome? pdfOutcome = result.ForTarget(OutputTarget.Pdf);
        ConversionOutcome? slaOutcome = result.ForTarget(OutputTarget.Sla);
        Assert.NotNull(pdfOutcome);
        Assert.NotNull(slaOutcome);
        Assert.True(pdfOutcome.Success);
        Assert.False(slaOutcome.Success);
        Assert.Equal(1, pdfEngine.CallCount);
    }

    [Fact]
    public void Convert_SlaOnly_WorksWithoutAPdfEngine()
    {
        FakeSlaConverter slaConverter = new FakeSlaConverter(ConversionOutcome.Ok());
        RowConverter converter = new RowConverter(null, new FakeOdgConverter(), slaConverter);
        ConversionRow row = CreateRow(@"C:\work\flyer.pub", (OutputTarget.Sla, @"C:\work\flyer.sla"));

        RowConversionResult result = converter.Convert(row, new[] { OutputTarget.Sla });

        ConversionOutcome? slaOutcome = result.ForTarget(OutputTarget.Sla);
        Assert.NotNull(slaOutcome);
        Assert.True(slaOutcome.Success);
    }

    [Fact]
    public void Convert_PdfWithoutAPdfEngine_FailsThatTargetWithClearMessage()
    {
        RowConverter converter = new RowConverter(null, new FakeOdgConverter());
        ConversionRow row = CreateRow(@"C:\work\flyer.pub", (OutputTarget.Pdf, @"C:\work\flyer.pdf"));

        RowConversionResult result = converter.Convert(row, new[] { OutputTarget.Pdf });

        ConversionOutcome? pdfOutcome = result.ForTarget(OutputTarget.Pdf);
        Assert.NotNull(pdfOutcome);
        Assert.False(pdfOutcome.Success);
        Assert.Contains("No conversion engine", pdfOutcome.ErrorMessage);
    }

    private static ConversionRow CreateRow(string sourcePath, params (OutputTarget Target, string Path)[] outputs)
    {
        SourceFile source = new SourceFile(sourcePath, System.IO.Path.GetFileName(sourcePath), 0, DateTime.UtcNow);
        List<OutputInfo> infos = new List<OutputInfo>();
        foreach ((OutputTarget target, string path) in outputs)
        {
            infos.Add(OutputInfo.FromFileSystem(target, path));
        }

        return new ConversionRow(source, infos);
    }

    private sealed class FakePdfEngine : IConversionEngine
    {
        private readonly ConversionOutcome _outcome;

        public FakePdfEngine(ConversionOutcome outcome)
        {
            this._outcome = outcome;
        }

        public int CallCount { get; private set; }

        public EngineKind Kind
        {
            get { return EngineKind.Publisher; }
        }

        public bool IsAvailable()
        {
            return true;
        }

        public ConversionOutcome ConvertToPdf(SourceFile source, string outputPdfPath)
        {
            this.CallCount++;
            return this._outcome;
        }
    }

    private sealed class FakeOdgConverter : IPdfToOdgConverter
    {
        public int CallCount { get; private set; }

        public bool IsAvailable()
        {
            return true;
        }

        public ConversionOutcome ConvertPdfToOdg(string sourcePdfPath, string outputOdgPath)
        {
            this.CallCount++;
            return ConversionOutcome.Ok();
        }
    }

    private sealed class FakeSlaConverter : IPubToSlaConverter
    {
        private readonly ConversionOutcome _outcome;

        public FakeSlaConverter(ConversionOutcome outcome)
        {
            this._outcome = outcome;
        }

        public string? LastOutputPath { get; private set; }

        public bool IsAvailable()
        {
            return true;
        }

        public ConversionOutcome ConvertToSla(SourceFile source, string outputSlaPath)
        {
            this.LastOutputPath = outputSlaPath;
            return this._outcome;
        }
    }
}
