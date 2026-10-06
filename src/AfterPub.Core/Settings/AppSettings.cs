using AfterPub.Core.Conversion;
using AfterPub.Core.Engines;
using AfterPub.Core.Scanning;

namespace AfterPub.Core.Settings;

/// <summary>
/// User-configurable settings that persist between runs. Deliberately minimal for
/// now: only what has a real UI wired to it today (CLAUDE.md section 8 lists more
/// settings, most of which depend on features — quality options, the record file —
/// that do not exist yet, and are added when those features are).
///
/// The overwrite choice (Ask / Skip / Overwrite) is deliberately NOT here: it is not saved, and
/// every start of the app begins with Ask.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Default state of the "include subfolders" checkbox on startup.</summary>
    public bool Recursive { get; set; }

    /// <summary>Whether outputs are written next to each source, or under a separate folder.</summary>
    public OutputLocationMode LocationMode { get; set; } = OutputLocationMode.SameFolder;

    /// <summary>The separate output root, when <see cref="LocationMode"/> is SeparateFolder.</summary>
    public string? SeparateOutputRoot { get; set; }

    /// <summary>Which engine to use for conversion. Auto picks the best available one.</summary>
    public EngineSelection EngineSelection { get; set; } = EngineSelection.Auto;

    /// <summary>
    /// An explicit path to soffice.exe, if the user set one. Optional — when empty,
    /// only the standard install locations are checked.
    /// </summary>
    public string? LibreOfficePath { get; set; }

    /// <summary>
    /// An explicit path to Scribus.exe, if the user set one. Optional — when empty,
    /// only the standard install locations are checked.
    /// </summary>
    public string? ScribusPath { get; set; }

    /// <summary>
    /// Which output types to scan for and produce. PDF-only by default; ODG needs
    /// LibreOffice and SLA needs Scribus, so both are off by default (CLAUDE.md section 3.2).
    /// These are the "Convert to" targets; a scan always reports every output type.
    /// </summary>
    public List<OutputTarget> EnabledTargets { get; set; } = new List<OutputTarget> { OutputTarget.Pdf };

    /// <summary>
    /// Light or dark appearance. Read once at startup, so a change applies after the app is
    /// restarted. Older settings files without this value open in light mode.
    /// </summary>
    public AppColorMode ColorMode { get; set; } = AppColorMode.Light;
}
