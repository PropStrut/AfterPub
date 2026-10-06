using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;

namespace AfterPub.Core.Conversion;

/// <summary>
/// Produces whichever output targets are wanted for one source file. Handles the
/// PDF/ODG relationship described in CLAUDE.md section 3.2: ODG is always generated
/// FROM a PDF via LibreOffice, regardless of which engine made that PDF. When PDF is
/// not itself a wanted target, the PDF used to feed ODG is a scratch file — produced,
/// used, then deleted — rather than a kept, tracked output.
///
/// SLA is independent of that chain: Scribus saves it straight from the .pub, so asking
/// for SLA never creates a PDF, and an SLA failure never affects the PDF or ODG outcomes.
/// </summary>
public sealed class RowConverter
{
    private readonly IConversionEngine? _pubToPdfEngine;
    private readonly IPdfToOdgConverter _odgConverter;
    private readonly IPubToSlaConverter? _slaConverter;

    /// <param name="pubToPdfEngine">
    /// Whichever engine (Publisher, LibreOffice or Scribus) was resolved for .pub -&gt; PDF. May be
    /// null when only SLA is wanted, since SLA does not use the PDF stage; asking for PDF or ODG
    /// without one fails those targets with a clear message.
    /// </param>
    /// <param name="odgConverter">The PDF -&gt; ODG capability. In practice always backed by LibreOffice.</param>
    /// <param name="slaConverter">
    /// The .pub -&gt; SLA capability, in practice always backed by Scribus. Optional so callers
    /// written before SLA existed still compile; asking for SLA without one fails that target
    /// with a clear message.
    /// </param>
    public RowConverter(
        IConversionEngine? pubToPdfEngine,
        IPdfToOdgConverter odgConverter,
        IPubToSlaConverter? slaConverter = null)
    {
        this._pubToPdfEngine = pubToPdfEngine;
        this._odgConverter = odgConverter;
        this._slaConverter = slaConverter;
    }

    /// <summary>
    /// Converts <paramref name="row"/>'s source to every target in
    /// <paramref name="wantedTargets"/>. Output paths for targets the row already has
    /// resolved (via a scan that included them) come from <see cref="ConversionRow.GetOutput"/>;
    /// a scratch PDF path is synthesized when PDF itself is not wanted but is needed
    /// as ODG's input.
    /// </summary>
    public RowConversionResult Convert(ConversionRow row, IReadOnlyList<OutputTarget> wantedTargets)
    {
        Dictionary<OutputTarget, ConversionOutcome> outcomes = new Dictionary<OutputTarget, ConversionOutcome>();

        bool wantsPdf = wantedTargets.Contains(OutputTarget.Pdf);
        bool wantsOdg = wantedTargets.Contains(OutputTarget.Odg);
        bool wantsSla = wantedTargets.Contains(OutputTarget.Sla);

        if (wantsSla)
        {
            // Saved straight from the .pub by Scribus; it does not use the PDF stage below.
            outcomes[OutputTarget.Sla] = this.ConvertToSla(row);
        }

        if (!wantsPdf && !wantsOdg)
        {
            return new RowConversionResult(outcomes);
        }

        string pdfPath;
        bool pdfIsScratch;

        if (wantsPdf)
        {
            OutputInfo? pdfOutput = row.GetOutput(OutputTarget.Pdf);
            if (pdfOutput is null)
            {
                outcomes[OutputTarget.Pdf] = ConversionOutcome.Failed("No PDF output path was resolved for this row.");
                if (!wantsOdg)
                {
                    return new RowConversionResult(outcomes);
                }

                pdfPath = BuildScratchPdfPath(row.Source);
                pdfIsScratch = true;
            }
            else
            {
                pdfPath = pdfOutput.ExpectedPath;
                pdfIsScratch = false;
            }
        }
        else
        {
            pdfPath = BuildScratchPdfPath(row.Source);
            pdfIsScratch = true;
        }

        try
        {
            ConversionOutcome pdfOutcome = this._pubToPdfEngine is null
                ? ConversionOutcome.Failed("No conversion engine was available for the PDF stage.")
                : this._pubToPdfEngine.ConvertToPdf(row.Source, pdfPath);
            if (wantsPdf && !outcomes.ContainsKey(OutputTarget.Pdf))
            {
                outcomes[OutputTarget.Pdf] = pdfOutcome;
            }

            if (wantsOdg)
            {
                outcomes[OutputTarget.Odg] = this.ConvertToOdg(row, pdfOutcome, pdfPath);
            }
        }
        finally
        {
            // The scratch PDF lives in its own temporary folder, which is removed with it.
            if (pdfIsScratch)
            {
                TryDeleteFolder(Path.GetDirectoryName(pdfPath));
            }
        }

        return new RowConversionResult(outcomes);
    }

    private ConversionOutcome ConvertToSla(ConversionRow row)
    {
        if (this._slaConverter is null || !this._slaConverter.IsAvailable())
        {
            return ConversionOutcome.Failed("Scribus is required to produce SLA output but was not found.");
        }

        OutputInfo? slaOutput = row.GetOutput(OutputTarget.Sla);
        if (slaOutput is null)
        {
            return ConversionOutcome.Failed("No SLA output path was resolved for this row.");
        }

        return this._slaConverter.ConvertToSla(row.Source, slaOutput.ExpectedPath);
    }

    private ConversionOutcome ConvertToOdg(ConversionRow row, ConversionOutcome pdfOutcome, string pdfPath)
    {
        if (!pdfOutcome.Success)
        {
            return ConversionOutcome.Failed($"Could not produce the PDF that ODG is generated from: {pdfOutcome.ErrorMessage}");
        }

        if (!this._odgConverter.IsAvailable())
        {
            return ConversionOutcome.Failed("LibreOffice is required to produce ODG output but was not found.");
        }

        OutputInfo? odgOutput = row.GetOutput(OutputTarget.Odg);
        if (odgOutput is null)
        {
            return ConversionOutcome.Failed("No ODG output path was resolved for this row.");
        }

        return this._odgConverter.ConvertPdfToOdg(pdfPath, odgOutput.ExpectedPath);
    }

    private static string BuildScratchPdfPath(SourceFile source)
    {
        // A folder of its own for every conversion, so the scratch PDF can never collide with
        // another scratch file, and can never be the same file as a real PDF somewhere else.
        string scratchFolder = Path.Combine(Path.GetTempPath(), "AfterPubScratch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratchFolder);

        // Named after the source, so the ODG made from it has the right base name.
        return Path.Combine(scratchFolder, source.BaseName + ".pdf");
    }

    private static void TryDeleteFolder(string? folder)
    {
        try
        {
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
        catch
        {
            // Best-effort; a leftover scratch folder in the temp directory is harmless.
        }
    }
}
