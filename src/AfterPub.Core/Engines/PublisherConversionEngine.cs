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
/// </summary>
public sealed class PublisherConversionEngine : IConversionEngine
{
    private const string ProgId = "Publisher.Application";

    // Publisher's PbFixedFormatType enum, confirmed against Microsoft's reference:
    // pbFixedFormatTypePDF = 2, pbFixedFormatTypeXPS = 1. (The earlier value of 1
    // here was wrong — it was actually the XPS constant.)
    private const int PbFixedFormatTypePdf = 2;

    private const BindingFlags InvokeMethodFlags =
        BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance;

    private const BindingFlags GetPropertyFlags =
        BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance;

    private const BindingFlags SetPropertyFlags =
        BindingFlags.SetProperty | BindingFlags.Public | BindingFlags.Instance;

    public EngineKind Kind => EngineKind.Publisher;

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

        object? application = null;
        object? document = null;
        string stage = "starting Publisher";

        try
        {
            application = Activator.CreateInstance(publisherType);
            if (application is null)
            {
                return ConversionOutcome.Failed("Could not start Publisher.");
            }

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
            stage = "hiding the Publisher window";
            TryHideActiveWindow(application);

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
                TryCallMethod(application, "Quit");
                ReleaseComObject(application);
            }
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
