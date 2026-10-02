using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;

namespace AfterPub.Core.Conversion;

/// <summary>
/// Produces whichever output targets are wanted for one source file. Handles the
/// PDF/ODG relationship described in CLAUDE.md section 3.2: ODG is always generated
/// FROM a PDF via LibreOffice, regardless of which engine made that PDF. When PDF is
/// not itself a wanted target, the PDF used to feed ODG is a scratch file — produced,
/// used, then deleted — rather than a kept, tracked output.
/// </summary>
public sealed class RowConverter
{
    private readonly IConversionEngine _pubToPdfEngine;
    private readonly IPdfToOdgConverter _odgConverter;

    /// <param name="pubToPdfEngine">Whichever engine (Publisher or LibreOffice) was resolved for .pub -&gt; PDF.</param>
    /// <param name="odgConverter">The PDF -&gt; ODG capability. In practice always backed by LibreOffice.</param>
    public RowConverter(IConversionEngine pubToPdfEngine, IPdfToOdgConverter odgConverter)
    {
        this._pubToPdfEngine = pubToPdfEngine;
        this._odgConverter = odgConverter;
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

        ConversionOutcome pdfOutcome = this._pubToPdfEngine.ConvertToPdf(row.Source, pdfPath);
        if (wantsPdf && !outcomes.ContainsKey(OutputTarget.Pdf))
        {
            outcomes[OutputTarget.Pdf] = pdfOutcome;
        }

        if (wantsOdg)
        {
            outcomes[OutputTarget.Odg] = this.ConvertToOdg(row, pdfOutcome, pdfPath);
        }

        if (pdfIsScratch)
        {
            TryDeleteFile(pdfPath);
        }

        return new RowConversionResult(outcomes);
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
        string scratchFolder = Path.Combine(Path.GetTempPath(), "AfterPubScratch");
        Directory.CreateDirectory(scratchFolder);

        // Named after the source, not a random GUID, so LibreOffice's --convert-to
        // (which names its output after the input) produces an ODG with the right
        // base name once it lands in the real output folder.
        return Path.Combine(scratchFolder, source.BaseName + ".pdf");
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort; a leftover scratch file in the temp folder is harmless.
        }
    }
}
