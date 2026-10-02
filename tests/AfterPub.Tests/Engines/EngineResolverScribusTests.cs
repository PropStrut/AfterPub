using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;

namespace AfterPub.Tests.Engines;

public sealed class EngineResolverScribusTests
{
    [Fact]
    public void Auto_AllAvailable_PrefersPublisher()
    {
        StubEngine publisher = new StubEngine(EngineKind.Publisher, true);
        StubEngine libreOffice = new StubEngine(EngineKind.LibreOffice, true);
        StubEngine scribus = new StubEngine(EngineKind.Scribus, true);

        IConversionEngine? result = new EngineResolver().ResolveEngine(EngineSelection.Auto, publisher, libreOffice, scribus);

        Assert.Same(publisher, result);
    }

    [Fact]
    public void Auto_PublisherMissing_PrefersLibreOfficeOverScribus()
    {
        StubEngine publisher = new StubEngine(EngineKind.Publisher, false);
        StubEngine libreOffice = new StubEngine(EngineKind.LibreOffice, true);
        StubEngine scribus = new StubEngine(EngineKind.Scribus, true);

        IConversionEngine? result = new EngineResolver().ResolveEngine(EngineSelection.Auto, publisher, libreOffice, scribus);

        Assert.Same(libreOffice, result);
    }

    [Fact]
    public void Auto_OnlyScribusAvailable_ReturnsScribus()
    {
        StubEngine publisher = new StubEngine(EngineKind.Publisher, false);
        StubEngine libreOffice = new StubEngine(EngineKind.LibreOffice, false);
        StubEngine scribus = new StubEngine(EngineKind.Scribus, true);

        IConversionEngine? result = new EngineResolver().ResolveEngine(EngineSelection.Auto, publisher, libreOffice, scribus);

        Assert.Same(scribus, result);
    }

    [Fact]
    public void Auto_NoneAvailable_ReturnsNull()
    {
        StubEngine publisher = new StubEngine(EngineKind.Publisher, false);
        StubEngine libreOffice = new StubEngine(EngineKind.LibreOffice, false);
        StubEngine scribus = new StubEngine(EngineKind.Scribus, false);

        IConversionEngine? result = new EngineResolver().ResolveEngine(EngineSelection.Auto, publisher, libreOffice, scribus);

        Assert.Null(result);
    }

    [Fact]
    public void ExplicitScribus_Available_ReturnsScribus_AndUnavailableReturnsNull()
    {
        StubEngine publisher = new StubEngine(EngineKind.Publisher, true);
        StubEngine libreOffice = new StubEngine(EngineKind.LibreOffice, true);
        StubEngine availableScribus = new StubEngine(EngineKind.Scribus, true);
        StubEngine missingScribus = new StubEngine(EngineKind.Scribus, false);
        EngineResolver resolver = new EngineResolver();

        Assert.Same(availableScribus, resolver.ResolveEngine(EngineSelection.Scribus, publisher, libreOffice, availableScribus));
        Assert.Null(resolver.ResolveEngine(EngineSelection.Scribus, publisher, libreOffice, missingScribus));
    }

    [Fact]
    public void ExplicitScribus_NoScribusEngineSupplied_ReturnsNull()
    {
        StubEngine publisher = new StubEngine(EngineKind.Publisher, true);
        StubEngine libreOffice = new StubEngine(EngineKind.LibreOffice, true);

        IConversionEngine? result = new EngineResolver().ResolveEngine(EngineSelection.Scribus, publisher, libreOffice);

        Assert.Null(result);
    }

    private sealed class StubEngine : IConversionEngine
    {
        private readonly bool _available;

        public StubEngine(EngineKind kind, bool available)
        {
            this.Kind = kind;
            this._available = available;
        }

        public EngineKind Kind { get; }

        public bool IsAvailable()
        {
            return this._available;
        }

        public ConversionOutcome ConvertToPdf(SourceFile source, string outputPdfPath)
        {
            throw new NotSupportedException();
        }
    }
}
