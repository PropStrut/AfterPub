namespace AfterPub.Core.Scanning;

/// <summary>
/// Settings that control one scan: where to look, how deep, which output types to
/// report on, and where those outputs are expected to live.
/// </summary>
public sealed class ScanOptions
{
    /// <summary>Absolute path to the folder to scan.</summary>
    public required string RootFolder { get; init; }

    /// <summary>True to include sub-directories; false for the top folder only.</summary>
    public bool Recursive { get; init; }

    /// <summary>Which output types to check the status of. Order here becomes column order in the UI.</summary>
    public required IReadOnlyList<OutputTarget> EnabledTargets { get; init; }

    /// <summary>Same-folder or separate-folder output location (CLAUDE.md section 8).</summary>
    public OutputLocationMode LocationMode { get; init; } = OutputLocationMode.SameFolder;

    /// <summary>
    /// Root of the separate output folder. Required when <see cref="LocationMode"/> is
    /// <see cref="OutputLocationMode.SeparateFolder"/>; ignored otherwise.
    /// </summary>
    public string? SeparateOutputRoot { get; init; }
}
