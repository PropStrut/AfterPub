using AfterPub.Core.Scanning;

namespace AfterPub.Core.Engines;

/// <summary>
/// The .pub -&gt; SLA stage (CLAUDE.md section 3.2): Scribus's native, editable format,
/// saved straight from the imported .pub (not from a PDF). This is specifically a Scribus
/// capability, independent of which engine produced any PDF. Kept as its own interface so
/// it can be faked in tests without a real Scribus install.
/// </summary>
public interface IPubToSlaConverter
{
    bool IsAvailable();

    /// <summary>
    /// Saves <paramref name="source"/> as a Scribus document at <paramref name="outputSlaPath"/>.
    /// Never throws for ordinary failures; they come back as a failed <see cref="ConversionOutcome"/>.
    /// </summary>
    ConversionOutcome ConvertToSla(SourceFile source, string outputSlaPath);
}
