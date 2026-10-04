using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using AfterPub.Core.Scanning;

namespace AfterPub.Core.Engines;

/// <summary>
/// Converts via Microsoft Publisher's COM automation. Uses late binding
/// (<see cref="Type.InvokeMember(string, BindingFlags, Binder?, object?, object?[]?)"/>)
/// rather than a compiled Office interop reference, so the app has no compile-time
/// Office dependency and still builds on a machine without Publisher installed
/// (CLAUDE.md section 3.1).
///
/// The exact Open/ExportAsFixedFormat parameter shapes below are written from
/// Microsoft's documented Publisher automation model but have not yet been fully
/// tested against a real installed copy — see CLAUDE.md section 15. If a call
/// throws a <see cref="MissingMemberException"/> or a COM "Unknown name" error
/// (0x80020006), that means the member name doesn't exist on that object as
/// written — check Microsoft's Publisher VBA reference for the correct one, the
/// way the Visible property below was fixed.
///
/// Safety around the user's own Publisher: Publisher is a separate process the app does not
/// own, and the user may have it open with their own documents. So the app only ever
/// stops, hides or quits a Publisher process that appeared while this engine was starting
/// its own instance. A Publisher that was already running is never killed, hidden or quit.
/// </summary>
public sealed class PublisherConversionEngine : IConversionEngine
{
    private const string ProgId = "Publisher.Application";

    // Publisher's executable is MSPUB.EXE, so its process name is "MSPUB".
    private const string PublisherProcessName = "MSPUB";

    // Publisher's PbFixedFormatType enum, confirmed against Microsoft's reference:
    // pbFixedFormatTypePDF = 2, pbFixedFormatTypeXPS = 1. (The earlier value of 1
    // here was wrong — it was actually the XPS constant.)
    private const int PbFixedFormatTypePdf = 2;

    // One file should take seconds. Three minutes leaves room for a very large document
    // while still stopping a conversion that is stuck, for example on a dialog nobody can see.
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromMinutes(3);

    private const int UnwindMilliseconds = 5000;

    private const BindingFlags InvokeMethodFlags =
        BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance;

    private const BindingFlags GetPropertyFlags =
        BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance;

    private const BindingFlags SetPropertyFlags =
        BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance;

    private readonly ProcessTracker? _tracker;

    public EngineKind Kind => EngineKind.Publisher;

    /// <param name="tracker">
    /// Optional. When supplied, the Publisher process this engine starts is registered with it
    /// so an "Abort now" can stop it immediately (CLAUDE.md section 3.4).
    /// </param>
    public PublisherConversionEngine(ProcessTracker? tracker = null)
    {
        this._tracker = tracker;
    }

    public bool IsAvailable()
    {
        return Type.GetTypeFromProgID(ProgId) is not null;
    }

    public ConversionOutcome ConvertToPdf(SourceFile source, string outputPdfPath)
    {
        Type? publisherType = Type.GetTypeFromProgID(ProgId);
        if (publisherType is null)
        {
            return ConversionOutcome.Failed("Publisher is not installed (Publisher.Application is not registered).");
        }

        // Which Publisher processes exist before we start one, so afterwards we can tell
        // exactly which ones are ours.
        HashSet<int> processesBefore = GetPublisherProcessIds();
        HashSet<int> ownedProcessIds = new HashSet<int>();
        bool outputExistedBefore = File.Exists(outputPdfPath);

        // The COM calls run on a worker so a stuck one (a dialog nobody can see) can be
        // timed out instead of hanging the whole batch.
        Task<ConversionOutcome> work = Task.Run(
            () => this.RunConversion(publisherType, source, outputPdfPath, processesBefore, ownedProcessIds));

        if (work.Wait(ConversionTimeout))
        {
            try
            {
                return work.Result;
            }
            catch (AggregateException ex)
            {
                return ConversionOutcome.Failed($"Publisher conversion failed unexpectedly: {ex.GetBaseException().Message}");
            }
        }

        bool stopped = KillOwnedProcesses(ownedProcessIds);

        // Killing the process makes the stuck COM call fail, which lets the worker unwind.
        try
        {
            work.Wait(UnwindMilliseconds);
        }
        catch (AggregateException)
        {
            // The worker's own failure no longer matters; the timeout is what gets reported.
        }

        if (!outputExistedBefore)
        {
            TryDeleteFile(outputPdfPath);
        }

        string minutes = ConversionTimeout.TotalMinutes.ToString("0");
        return stopped
            ? ConversionOutcome.Failed($"Publisher did not finish within {minutes} minutes and was stopped.")
            : ConversionOutcome.Failed($"Publisher did not finish within {minutes} minutes, and it could not be stopped automatically.");
    }

    private ConversionOutcome RunConversion(
        Type publisherType,
        SourceFile source,
        string outputPdfPath,
        HashSet<int> processesBefore,
        HashSet<int> ownedProcessIds)
    {
        object? application = null;
        object? document = null;
        List<Process> trackedProcesses = new List<Process>();
        bool startedByUs = false;
        bool outputExistedBefore = File.Exists(outputPdfPath);
        string stage = "starting Publisher";

        try
        {
            application = Activator.CreateInstance(publisherType);
            if (application is null)
            {
                return ConversionOutcome.Failed("Could not start Publisher.");
            }

            startedByUs = this.RegisterStartedProcesses(processesBefore, ownedProcessIds, trackedProcesses);

            // Open(FileName, ReadOnly, AddToRecentFiles) — to verify.
            stage = "opening the document";
            document = CallMethod(application, "Open", source.FullPath, false, false);
            if (document is null)
            {
                return ConversionOutcome.Failed("Publisher did not return a document after Open.");
            }

            // Application itself has no Visible property — it lives on ActiveWindow.
            // A fresh automation instance may already start hidden, so this is only
            // a best-effort precaution; a failure here should not fail the conversion.
            // Only done for an instance we started: if we ended up attached to the user's
            // own running Publisher, hiding its window would hide their work.
            if (startedByUs)
            {
                stage = "hiding the Publisher window";
                TryHideActiveWindow(application);
            }

            string? outputFolder = Path.GetDirectoryName(outputPdfPath);
            if (!string.IsNullOrEmpty(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }

            stage = "exporting to PDF";
            CallMethod(document, "ExportAsFixedFormat", PbFixedFormatTypePdf, outputPdfPath);

            stage = "closing the document";
            CallMethod(document, "Close");
            document = null;

            return File.Exists(outputPdfPath)
                ? ConversionOutcome.Ok()
                : ConversionOutcome.Failed("Publisher did not report an error, but no PDF was produced.");
        }
        catch (Exception ex) when (ex is COMException or TargetInvocationException or MissingMemberException)
        {
            // An abort kills the Publisher process, which makes the COM call fail with an
            // "RPC server unavailable" style error. Report that as a stop, not a Publisher failure.
            if (this._tracker is { AbortRequested: true })
            {
                if (!outputExistedBefore)
                {
                    TryDeleteFile(outputPdfPath);
                }

                return ConversionOutcome.Failed(ProcessTracker.AbortedMessage);
            }

            return ConversionOutcome.Failed($"Publisher automation failed while {stage}: {DescribeException(ex)}");
        }
        finally
        {
            if (document is not null)
            {
                TryCallMethod(document, "Close");
                ReleaseComObject(document);
            }

            if (application is not null)
            {
                // Quit only an instance this engine started; never the user's own Publisher.
                if (startedByUs)
                {
                    TryCallMethod(application, "Quit");
                }

                ReleaseComObject(application);
            }

            foreach (Process process in trackedProcesses)
            {
                this._tracker?.Untrack(process);
                process.Dispose();
            }
        }
    }

    /// <summary>
    /// Works out which Publisher processes appeared since the snapshot, remembers their ids
    /// (so a timeout can stop exactly those) and registers them for "Abort now". Returns
    /// whether this engine started the instance it is driving: true if a new Publisher process
    /// appeared, or if no Publisher was running beforehand (so the instance must be ours even
    /// if the process could not be identified). Returns false only when a Publisher was already
    /// running and no new one appeared, meaning the automation attached to the user's own copy.
    /// </summary>
    private bool RegisterStartedProcesses(
        HashSet<int> processesBefore,
        HashSet<int> ownedProcessIds,
        List<Process> trackedProcesses)
    {
        try
        {
            foreach (Process process in Process.GetProcessesByName(PublisherProcessName))
            {
                if (processesBefore.Contains(process.Id))
                {
                    process.Dispose();
                    continue;
                }

                lock (ownedProcessIds)
                {
                    ownedProcessIds.Add(process.Id);
                }

                trackedProcesses.Add(process);
                this._tracker?.Track(process);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Process enumeration is a safety feature, never a reason to fail a conversion.
        }

        lock (ownedProcessIds)
        {
            return ownedProcessIds.Count > 0 || processesBefore.Count == 0;
        }
    }

    private static HashSet<int> GetPublisherProcessIds()
    {
        HashSet<int> ids = new HashSet<int>();

        try
        {
            foreach (Process process in Process.GetProcessesByName(PublisherProcessName))
            {
                ids.Add(process.Id);
                process.Dispose();
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // Treated as "none known"; see RegisterStartedProcesses.
        }

        return ids;
    }

    private static bool KillOwnedProcesses(HashSet<int> ownedProcessIds)
    {
        List<int> ids;
        lock (ownedProcessIds)
        {
            ids = new List<int>(ownedProcessIds);
        }

        bool stoppedAny = false;
        foreach (int id in ids)
        {
            try
            {
                using Process process = Process.GetProcessById(id);
                process.Kill(entireProcessTree: true);
                stoppedAny = true;
            }
            catch
            {
                // Already gone, or not ours to stop any more; nothing more to do.
            }
        }

        return stoppedAny;
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
            // Best-effort; a leftover partial file is harmless next to the failure being reported.
        }
    }

    /// <summary>
    /// Type.InvokeMember wraps the real COM error in a TargetInvocationException;
    /// this unwraps it so the failure message actually says what went wrong.
    /// </summary>
    private static string DescribeException(Exception ex)
    {
        Exception effective = ex;
        while (effective is TargetInvocationException { InnerException: Exception inner })
        {
            effective = inner;
        }

        if (effective is COMException comEx)
        {
            return $"{effective.Message} (HRESULT 0x{comEx.HResult:X8})";
        }

        return effective.Message;
    }

    private static void TryHideActiveWindow(object application)
    {
        object? activeWindow = null;
        try
        {
            activeWindow = GetProperty(application, "ActiveWindow");
            if (activeWindow is not null)
            {
                SetProperty(activeWindow, "Visible", false);
            }
        }
        catch
        {
            // Best-effort only; not every Publisher version/state exposes this
            // right after Open, and hiding the window is a nicety, not a requirement.
        }
        finally
        {
            if (activeWindow is not null)
            {
                ReleaseComObject(activeWindow);
            }
        }
    }

    private static object? CallMethod(object target, string name, params object?[] args)
    {
        return target.GetType().InvokeMember(name, InvokeMethodFlags, binder: null, target, args);
    }

    private static void TryCallMethod(object target, string name, params object?[] args)
    {
        try
        {
            CallMethod(target, name, args);
        }
        catch
        {
            // Best-effort cleanup; the primary error (if any) has already been captured.
        }
    }

    private static object? GetProperty(object target, string name)
    {
        return target.GetType().InvokeMember(name, GetPropertyFlags, binder: null, target, Array.Empty<object?>());
    }

    private static void SetProperty(object target, string name, object? value)
    {
        target.GetType().InvokeMember(name, SetPropertyFlags, binder: null, target, new[] { value });
    }

    private static void ReleaseComObject(object comObject)
    {
        if (Marshal.IsComObject(comObject))
        {
            Marshal.FinalReleaseComObject(comObject);
        }
    }
}
