using AfterPub.Core.Scanning;

namespace AfterPub.Core.Engines;

/// <summary>
/// One way of turning a .pub file into a PDF. Implementations sit behind this
/// interface so tests can use fakes (CLAUDE.md section 12) — the test suite must
/// not require Publisher or LibreOffice to actually be installed.
/// </summary>
public interface IConversionEngine
{
    EngineKind Kind { get; }

    /// <summary>Cheap, fast check for whether this engine can be used right now.</summary>
    bool IsAvailable();

    /// <summary>
    /// Converts <paramref name="source"/> to a PDF at <paramref name="outputPdfPath"/>.
    /// Never throws for ordinary failures (missing file, engine error, etc.) — those
    /// come back as a failed <see cref="ConversionOutcome"/> so a batch can continue
    /// past one bad file (CLAUDE.md section 3.4).
    /// </summary>
    ConversionOutcome ConvertToPdf(SourceFile source, string outputPdfPath);
}
