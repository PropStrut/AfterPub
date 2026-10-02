using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;
using Xunit;

namespace AfterPub.Tests.Engines;

/// <summary>Simple test double so EngineResolver can be tested without real Publisher or LibreOffice.</summary>
internal sealed class FakeConversionEngine : IConversionEngine
{
    private readonly bool _isAvailable;

    public EngineKind Kind { get; }

    public FakeConversionEngine(EngineKind kind, bool isAvailable)
    {
        this.Kind = kind;
        this._isAvailable = isAvailable;
    }

    public bool IsAvailable()
    {
        return this._isAvailable;
    }

    public ConversionOutcome ConvertToPdf(SourceFile source, string outputPdfPath)
    {
        return ConversionOutcome.Ok();
    }
}

public class EngineResolverTests
{
    [Fact]
    public void ResolveEngine_Auto_PrefersPublisher_WhenBothAvailable()
    {
        EngineResolver resolver = new EngineResolver();
        FakeConversionEngine publisher = new FakeConversionEngine(EngineKind.Publisher, isAvailable: true);
        FakeConversionEngine libreOffice = new FakeConversionEngine(EngineKind.LibreOffice, isAvailable: true);

        IConversionEngine? result = resolver.ResolveEngine(EngineSelection.Auto, publisher, libreOffice);

        Assert.Same(publisher, result);
    }

    [Fact]
    public void ResolveEngine_Auto_FallsBackToLibreOffice_WhenPublisherUnavailable()
    {
        EngineResolver resolver = new EngineResolver();
        FakeConversionEngine publisher = new FakeConversionEngine(EngineKind.Publisher, isAvailable: false);
        FakeConversionEngine libreOffice = new FakeConversionEngine(EngineKind.LibreOffice, isAvailable: true);

        IConversionEngine? result = resolver.ResolveEngine(EngineSelection.Auto, publisher, libreOffice);

        Assert.Same(libreOffice, result);
    }

    [Fact]
    public void ResolveEngine_Auto_ReturnsNull_WhenNeitherAvailable()
    {
        EngineResolver resolver = new EngineResolver();
        FakeConversionEngine publisher = new FakeConversionEngine(EngineKind.Publisher, isAvailable: false);
        FakeConversionEngine libreOffice = new FakeConversionEngine(EngineKind.LibreOffice, isAvailable: false);

        IConversionEngine? result = resolver.ResolveEngine(EngineSelection.Auto, publisher, libreOffice);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveEngine_ExplicitPublisher_ReturnsNull_WhenPublisherUnavailable_EvenIfLibreOfficeIs()
    {
        EngineResolver resolver = new EngineResolver();
        FakeConversionEngine publisher = new FakeConversionEngine(EngineKind.Publisher, isAvailable: false);
        FakeConversionEngine libreOffice = new FakeConversionEngine(EngineKind.LibreOffice, isAvailable: true);

        IConversionEngine? result = resolver.ResolveEngine(EngineSelection.Publisher, publisher, libreOffice);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveEngine_ExplicitLibreOffice_ReturnsLibreOffice_EvenIfPublisherIsAvailable()
    {
        EngineResolver resolver = new EngineResolver();
        FakeConversionEngine publisher = new FakeConversionEngine(EngineKind.Publisher, isAvailable: true);
        FakeConversionEngine libreOffice = new FakeConversionEngine(EngineKind.LibreOffice, isAvailable: true);

        IConversionEngine? result = resolver.ResolveEngine(EngineSelection.LibreOffice, publisher, libreOffice);

        Assert.Same(libreOffice, result);
    }
}
