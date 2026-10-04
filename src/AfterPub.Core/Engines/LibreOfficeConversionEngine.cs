using System.Diagnostics;
using AfterPub.Core.Scanning;

namespace AfterPub.Core.Engines;

/// <summary>
/// Converts by running LibreOffice headless. LibreOffice is never bundled (CLAUDE.md
/// section 3.1) — this only detects an existing install: a path from settings first,
/// then standard install locations. Runs with its own profile folder so it does not
/// collide with a copy of LibreOffice the user already has open, and enforces a
/// timeout so a stuck process cannot hang a batch indefinitely.
///
/// Implements two capabilities that both go through the same headless process:
/// <see cref="IConversionEngine"/> for .pub -&gt; PDF, and
/// <see cref="IPdfToOdgConverter"/> for the PDF -&gt; ODG stage (CLAUDE.md section 3.2).
/// </summary>
public sealed class LibreOfficeConversionEngine : IConversionEngine, IPdfToOdgConverter
{
    private static readonly string[] StandardInstallPaths =
    {
        @"C:\Program Files\LibreOffice\program\soffice.exe",
        @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
    };

    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromSeconds(90);

    private const string PdfConvertToArgument = "pdf";
    private const string OdgConvertToArgument = "odg";

    // Found wrong by real testing: "draw_pdf_import" is an IMPORT filter (which
    // component opens the PDF) and belongs on --infilter, not inside --convert-to
    // (which expects an EXPORT filter name). Putting an import filter name there
    // caused LibreOffice to fail writing the output ("impl_store... failed"),
    // because no export filter by that name exists. PDF already opens into Draw by
    // default, so this --infilter is mostly belt-and-braces explicitness.
    private const string PdfImportFilter = "draw_pdf_import";

    private readonly string? _configuredPath;
    private readonly ProcessTracker? _tracker;

    public EngineKind Kind => EngineKind.LibreOffice;

    /// <summary>The soffice.exe path this engine resolved to, or null if none was found.</summary>
    public string? ResolvedExecutablePath => ResolveExecutablePath(this._configuredPath, StandardInstallPaths, File.Exists);

    /// <param name="configuredPath">
    /// An explicit path to soffice.exe from settings, or null/empty to rely on
    /// standard install locations only.
    /// </param>
    /// <param name="tracker">
    /// Optional. When supplied, the LibreOffice process is registered with it so an
    /// "Abort now" can stop the process immediately (CLAUDE.md section 3.4).
    /// </param>
    public LibreOfficeConversionEngine(string? configuredPath, ProcessTracker? tracker = null)
    {
        this._configuredPath = configuredPath;
        this._tracker = tracker;
    }

    public bool IsAvailable()
    {
        return this.ResolvedExecutablePath is not null;
    }

    public ConversionOutcome ConvertToPdf(SourceFile source, string outputPdfPath)
    {
        return this.RunHeadlessConversion(source.FullPath, PdfConvertToArgument, outputPdfPath, inFilter: null);
    }

    public ConversionOutcome ConvertPdfToOdg(string sourcePdfPath, string outputOdgPath)
    {
        return this.RunHeadlessConversion(sourcePdfPath, OdgConvertToArgument, outputOdgPath, PdfImportFilter);
    }

    private ConversionOutcome RunHeadlessConversion(
        string inputFilePath,
        string convertToArgument,
        string expectedOutputPath,
        string? inFilter)
    {
        string? executablePath = this.ResolvedExecutablePath;
        if (executablePath is null)
        {
            return ConversionOutcome.Failed("LibreOffice was not found (no configured path, and none of the standard install locations exist).");
        }

        string? outputFolder = Path.GetDirectoryName(expectedOutputPath);
        if (string.IsNullOrEmpty(outputFolder))
        {
            return ConversionOutcome.Failed($"Could not determine an output folder for '{expectedOutputPath}'.");
        }

        Directory.CreateDirectory(outputFolder);

        // Remembered so an abort can discard a partial output without deleting a good one
        // that was already there.
        bool outputExistedBefore = File.Exists(expectedOutputPath);

        string profileFolder = Path.Combine(Path.GetTempPath(), "AfterPubLibreOfficeProfile");
        Directory.CreateDirectory(profileFolder);
        string profileUri = new Uri(profileFolder).AbsoluteUri;

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add($"-env:UserInstallation={profileUri}");

        if (!string.IsNullOrEmpty(inFilter))
        {
            startInfo.ArgumentList.Add($"--infilter={inFilter}");
        }

        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add(convertToArgument);
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outputFolder);
        startInfo.ArgumentList.Add(inputFilePath);

        using Process process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();
            this._tracker?.Track(process);

            bool exited = process.WaitForExit((int)ConversionTimeout.TotalMilliseconds);

            if (this._tracker is { AbortRequested: true })
            {
                if (!outputExistedBefore)
                {
                    TryDeleteFile(expectedOutputPath);
                }

                return ConversionOutcome.Failed(ProcessTracker.AbortedMessage);
            }

            if (!exited)
            {
                TryKill(process);
                return ConversionOutcome.Failed($"LibreOffice did not finish within {ConversionTimeout.TotalSeconds:0} seconds and was stopped.");
            }

            if (process.ExitCode != 0)
            {
                string stderr = process.StandardError.ReadToEnd();
                return ConversionOutcome.Failed($"LibreOffice exited with code {process.ExitCode}: {stderr}");
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return ConversionOutcome.Failed($"Could not run LibreOffice: {ex.Message}");
        }
        finally
        {
            this._tracker?.Untrack(process);
        }

        return File.Exists(expectedOutputPath)
            ? ConversionOutcome.Ok()
            : ConversionOutcome.Failed("LibreOffice exited without error, but the expected output file was not produced.");
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
            // Best-effort; a leftover partial file is reported by its absence of a result anyway.
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch
        {
            // Best-effort; the timeout failure is already being reported.
        }
    }

    /// <summary>
    /// Pure resolution logic, separated from <see cref="File.Exists(string?)"/> so it
    /// can be unit-tested without a real LibreOffice install (CLAUDE.md section 12).
    /// </summary>
    public static string? ResolveExecutablePath(
        string? configuredPath,
        IReadOnlyList<string> standardPaths,
        Func<string, bool> pathExists)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath) && pathExists(configuredPath))
        {
            return configuredPath;
        }

        foreach (string candidate in standardPaths)
        {
            if (pathExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
