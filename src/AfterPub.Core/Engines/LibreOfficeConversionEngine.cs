using System.Diagnostics;
using AfterPub.Core.Scanning;

namespace AfterPub.Core.Engines;

/// <summary>
/// Converts by running LibreOffice headless. LibreOffice is never bundled (CLAUDE.md
/// section 3.1) — this only detects an existing install: a path from settings first,
/// then standard install locations. Runs with its own profile folder so it does not
/// collide with a copy of LibreOffice the user already has open, and enforces a
/// timeout so a stuck process cannot hang a batch indefinitely.
/// Output goes to a private temporary folder and is moved into place only on success, so a
/// failed or stopped conversion never leaves a partial file or damages an existing one.
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

        if (string.IsNullOrEmpty(Path.GetDirectoryName(expectedOutputPath)))
        {
            return ConversionOutcome.Failed($"Could not determine an output folder for '{expectedOutputPath}'.");
        }

        // LibreOffice writes into a private temporary folder, never into the real output folder:
        // a conversion that fails or is stopped part-way can then never leave a partial file, or
        // damage a good file that is already there. Only a finished file is moved into place.
        string workFolder = Path.Combine(Path.GetTempPath(), "AfterPubLibreOffice_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(workFolder);

            // --convert-to names its output after the input file, with the new extension.
            string producedPath = Path.Combine(
                workFolder,
                Path.GetFileNameWithoutExtension(inputFilePath) + Path.GetExtension(expectedOutputPath));

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
            startInfo.ArgumentList.Add(workFolder);
            startInfo.ArgumentList.Add(inputFilePath);

            using Process process = new Process { StartInfo = startInfo };

            try
            {
                process.Start();
                this._tracker?.Track(process);

                bool exited = process.WaitForExit((int)ConversionTimeout.TotalMilliseconds);

                if (this._tracker is { AbortRequested: true })
                {
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
            finally
            {
                this._tracker?.Untrack(process);
            }

            if (!File.Exists(producedPath))
            {
                return ConversionOutcome.Failed("LibreOffice exited without error, but the expected output file was not produced.");
            }

            try
            {
                OutputFileMover.MoveIntoPlace(producedPath, expectedOutputPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return ConversionOutcome.Failed($"LibreOffice finished, but the result could not be saved to '{expectedOutputPath}': {ex.Message}");
            }

            return ConversionOutcome.Ok();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            return ConversionOutcome.Failed($"Could not run LibreOffice: {ex.Message}");
        }
        finally
        {
            TryDeleteFolder(workFolder);
        }
    }

    private static void TryDeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
            }
        }
        catch
        {
            // A leftover temp folder is harmless; never let cleanup fail a conversion.
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
