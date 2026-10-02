using System.Diagnostics;
using AfterPub.Core.Scanning;

namespace AfterPub.Core.Engines;

/// <summary>
/// Converts .pub to PDF by running Scribus headless with a small Python script
/// (Scribus's Scripter API). Like LibreOffice, Scribus is never bundled: this only
/// detects an existing install through <see cref="ScribusLocator"/>.
///
/// Differences from the LibreOffice engine, all deliberate:
/// - Paths reach the script through environment variables, not command-line
///   arguments, so nothing depends on how Scribus forwards arguments to scripts.
/// - The script reports its outcome by writing a small result file. Scribus can
///   keep running after a script finishes, or show a dialog that headless mode does
///   not suppress, so this engine stops waiting as soon as the result file appears
///   and kills the process if it lingers or exceeds the timeout.
/// - The PDF is written to a temporary folder and moved into place only on
///   success, so an existing output file always means a complete one
///   (CLAUDE.md section 3.3).
/// </summary>
public sealed class ScribusConversionEngine : IConversionEngine
{
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromSeconds(90);

    private const int PollIntervalMilliseconds = 200;
    private const int ExitGraceMilliseconds = 5000;

    private const string InputVariable = "AFTERPUB_INPUT";
    private const string OutputVariable = "AFTERPUB_OUTPUT";
    private const string ResultVariable = "AFTERPUB_RESULT";

    // The Scripter API calls used here (openDoc, PDFfile, save, closeDoc) are documented
    // by Scribus; whether openDoc accepts a .pub file directly is confirmed by real
    // testing, not assumed (CLAUDE.md section 15).
    private const string ScriptText = """
        import os
        import scribus

        def write_result(path, message):
            # Write to a temporary name and rename, so the C# side never reads a half-written file.
            temp_path = path + ".tmp"
            with open(temp_path, "w", encoding="utf-8") as handle:
                handle.write(message)
            os.replace(temp_path, path)

        source = os.environ["AFTERPUB_INPUT"]
        output = os.environ["AFTERPUB_OUTPUT"]
        result = os.environ["AFTERPUB_RESULT"]

        try:
            scribus.openDoc(source)
            pdf = scribus.PDFfile()
            pdf.file = output
            pdf.save()
            scribus.closeDoc()
            write_result(result, "OK")
        except Exception as error:
            try:
                scribus.closeDoc()
            except Exception:
                pass
            write_result(result, "ERROR: " + str(error))
        """;

    private readonly string? _configuredPath;
    private readonly ScribusLocator _locator;

    public EngineKind Kind => EngineKind.Scribus;

    /// <summary>The Scribus.exe path this engine resolved to, or null if none was found.</summary>
    public string? ResolvedExecutablePath => this._locator.Locate(this._configuredPath);

    /// <param name="configuredPath">
    /// An explicit path to Scribus.exe from settings, or null/empty to rely on the
    /// standard install locations only.
    /// </param>
    public ScribusConversionEngine(string? configuredPath)
        : this(configuredPath, new ScribusLocator())
    {
    }

    /// <summary>Lets tests supply a locator pointed at temporary folders.</summary>
    public ScribusConversionEngine(string? configuredPath, ScribusLocator locator)
    {
        this._configuredPath = configuredPath;
        this._locator = locator;
    }

    public bool IsAvailable()
    {
        return this.ResolvedExecutablePath is not null;
    }

    public ConversionOutcome ConvertToPdf(SourceFile source, string outputPdfPath)
    {
        string? executablePath = this.ResolvedExecutablePath;
        if (executablePath is null)
        {
            return ConversionOutcome.Failed("Scribus was not found (no configured path, and no Scribus folder in Program Files).");
        }

        string? outputFolder = Path.GetDirectoryName(outputPdfPath);
        if (string.IsNullOrEmpty(outputFolder))
        {
            return ConversionOutcome.Failed($"Could not determine an output folder for '{outputPdfPath}'.");
        }

        string workFolder = Path.Combine(Path.GetTempPath(), "AfterPubScribus_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(outputFolder);
            Directory.CreateDirectory(workFolder);

            string scriptPath = Path.Combine(workFolder, "convert.py");
            string tempPdfPath = Path.Combine(workFolder, "output.pdf");
            string resultPath = Path.Combine(workFolder, "result.txt");
            File.WriteAllText(scriptPath, ScriptText);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-g");
            startInfo.ArgumentList.Add("-py");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.Environment[InputVariable] = source.FullPath;
            startInfo.Environment[OutputVariable] = tempPdfPath;
            startInfo.Environment[ResultVariable] = resultPath;

            using Process process = new Process { StartInfo = startInfo };
            process.Start();

            Stopwatch stopwatch = Stopwatch.StartNew();
            while (!process.HasExited && !File.Exists(resultPath) && stopwatch.Elapsed < ConversionTimeout)
            {
                Thread.Sleep(PollIntervalMilliseconds);
            }

            if (!process.HasExited && !File.Exists(resultPath))
            {
                TryKill(process);
                return ConversionOutcome.Failed($"Scribus did not finish within {ConversionTimeout.TotalSeconds:0} seconds and was stopped.");
            }

            // The script has reported (or Scribus exited). Give it a moment to exit by
            // itself, then stop it if it is still running.
            if (!process.HasExited && !process.WaitForExit(ExitGraceMilliseconds))
            {
                TryKill(process);
            }

            if (!File.Exists(resultPath))
            {
                return ConversionOutcome.Failed($"Scribus exited without reporting a result (exit code {process.ExitCode}).");
            }

            string result = File.ReadAllText(resultPath).Trim();
            if (result != "OK")
            {
                return ConversionOutcome.Failed($"Scribus reported: {result}");
            }

            if (!File.Exists(tempPdfPath))
            {
                return ConversionOutcome.Failed("Scribus reported success, but the PDF was not produced.");
            }

            File.Move(tempPdfPath, outputPdfPath, overwrite: true);
            return ConversionOutcome.Ok();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException)
        {
            return ConversionOutcome.Failed($"Could not run Scribus: {ex.Message}");
        }
        finally
        {
            TryDeleteFolder(workFolder);
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
            // Best-effort; the failure is already being reported.
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
}
