using System.Diagnostics;
using AfterPub.Core.Scanning;

namespace AfterPub.Core.Engines;

/// <summary>
/// Runs Scribus headless with a small Python script (Scribus's Scripter API). Like
/// LibreOffice, Scribus is never bundled: this only detects an existing install through
/// <see cref="ScribusLocator"/>.
///
/// Two capabilities, both through the same headless process:
/// <see cref="IConversionEngine"/> for .pub -&gt; PDF, and
/// <see cref="IPubToSlaConverter"/> for .pub -&gt; SLA (Scribus's native format, saved
/// straight from the imported .pub; CLAUDE.md section 3.2).
///
/// Differences from the LibreOffice engine, all deliberate:
/// - Paths reach the script through environment variables, not command-line
///   arguments, so nothing depends on how Scribus forwards arguments to scripts.
/// - The script reports its outcome by writing a small result file. Scribus can
///   keep running after a script finishes, or show a dialog that headless mode does
///   not suppress, so this engine stops waiting as soon as the result file appears
///   and kills the process if it lingers or exceeds the timeout.
/// - The output is written to a temporary folder and moved into place only on
///   success, so an existing output file always means a complete one
///   (CLAUDE.md section 3.3).
/// </summary>
public sealed class ScribusConversionEngine : IConversionEngine, IPubToSlaConverter
{
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromSeconds(90);

    private const int PollIntervalMilliseconds = 200;
    private const int ExitGraceMilliseconds = 5000;

    private const string InputVariable = "AFTERPUB_INPUT";
    private const string OutputVariable = "AFTERPUB_OUTPUT";
    private const string ResultVariable = "AFTERPUB_RESULT";

    // Shared start of every script: read the three paths and define the result writer.
    private const string ScriptHeader = """
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
        """;

    // The Scripter API calls used here (openDoc, PDFfile, save, saveDocAs, closeDoc) are
    // documented by Scribus. openDoc accepting a .pub directly and PDFfile output are
    // confirmed by real testing; saveDocAs writing a usable .sla is confirmed the same
    // way or not at all (CLAUDE.md section 15).
    private const string PdfScriptBody = """
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

    private const string SlaScriptBody = """
        try:
            scribus.openDoc(source)
            scribus.saveDocAs(output)
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
    private readonly ProcessTracker? _tracker;

    public EngineKind Kind => EngineKind.Scribus;

    /// <summary>The Scribus.exe path this engine resolved to, or null if none was found.</summary>
    public string? ResolvedExecutablePath => this._locator.Locate(this._configuredPath);

    /// <param name="configuredPath">
    /// An explicit path to Scribus.exe from settings, or null/empty to rely on the
    /// standard install locations only.
    /// </param>
    /// <param name="tracker">
    /// Optional. When supplied, the Scribus process is registered with it so an
    /// "Abort now" can stop the process immediately (CLAUDE.md section 3.4).
    /// </param>
    public ScribusConversionEngine(string? configuredPath, ProcessTracker? tracker = null)
        : this(configuredPath, new ScribusLocator(), tracker)
    {
    }

    /// <summary>Lets tests supply a locator pointed at temporary folders.</summary>
    public ScribusConversionEngine(string? configuredPath, ScribusLocator locator, ProcessTracker? tracker = null)
    {
        this._configuredPath = configuredPath;
        this._locator = locator;
        this._tracker = tracker;
    }

    public bool IsAvailable()
    {
        return this.ResolvedExecutablePath is not null;
    }

    public ConversionOutcome ConvertToPdf(SourceFile source, string outputPdfPath)
    {
        return this.RunScript(BuildScript(PdfScriptBody), source.FullPath, outputPdfPath);
    }

    public ConversionOutcome ConvertToSla(SourceFile source, string outputSlaPath)
    {
        return this.RunScript(BuildScript(SlaScriptBody), source.FullPath, outputSlaPath);
    }

    private static string BuildScript(string body)
    {
        return ScriptHeader + "\n\n" + body + "\n";
    }

    private ConversionOutcome RunScript(string scriptText, string inputPath, string finalOutputPath)
    {
        string? executablePath = this.ResolvedExecutablePath;
        if (executablePath is null)
        {
            return ConversionOutcome.Failed("Scribus was not found (no configured path, and no Scribus folder in Program Files).");
        }

        string? outputFolder = Path.GetDirectoryName(finalOutputPath);
        if (string.IsNullOrEmpty(outputFolder))
        {
            return ConversionOutcome.Failed($"Could not determine an output folder for '{finalOutputPath}'.");
        }

        string workFolder = Path.Combine(Path.GetTempPath(), "AfterPubScribus_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(workFolder);

            string scriptPath = Path.Combine(workFolder, "convert.py");
            string tempOutputPath = Path.Combine(workFolder, "output" + Path.GetExtension(finalOutputPath));
            string resultPath = Path.Combine(workFolder, "result.txt");
            File.WriteAllText(scriptPath, scriptText);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("-g");
            startInfo.ArgumentList.Add("-py");
            startInfo.ArgumentList.Add(scriptPath);
            startInfo.Environment[InputVariable] = inputPath;
            startInfo.Environment[OutputVariable] = tempOutputPath;
            startInfo.Environment[ResultVariable] = resultPath;

            using Process process = new Process { StartInfo = startInfo };
            process.Start();

            this._tracker?.Track(process);
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                while (!process.HasExited && !File.Exists(resultPath) && stopwatch.Elapsed < ConversionTimeout)
                {
                    Thread.Sleep(PollIntervalMilliseconds);
                }

                if (this._tracker is { AbortRequested: true })
                {
                    return ConversionOutcome.Failed(ProcessTracker.AbortedMessage);
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

                if (!File.Exists(tempOutputPath))
                {
                    return ConversionOutcome.Failed("Scribus reported success, but the output file was not produced.");
                }

                OutputFileMover.MoveIntoPlace(tempOutputPath, finalOutputPath);
                return ConversionOutcome.Ok();
            }
            finally
            {
                this._tracker?.Untrack(process);
            }
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
