namespace AfterPub.Core.Engines;

/// <summary>
/// The PDF -&gt; ODG stage (CLAUDE.md section 3.2). This is specifically a LibreOffice
/// capability, independent of which engine produced the PDF in the first place —
/// a PDF from Publisher or from LibreOffice both feed this the same way. Kept as
/// its own interface (rather than folded into <see cref="IConversionEngine"/>) so
/// it can be faked in tests without a real LibreOffice install.
/// </summary>
public interface IPdfToOdgConverter
{
    bool IsAvailable();

    ConversionOutcome ConvertPdfToOdg(string sourcePdfPath, string outputOdgPath);
}
