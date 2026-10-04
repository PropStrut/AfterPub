namespace AfterPub.Core.Scanning;

/// <summary>
/// Settings that control one scan: where to look, how deep, which output types to
/// report on, and where those outputs are expected to live.
/// </summary>
public sealed class ScanOptions
{
    // Every output type AfterPub knows about. A scan reports on all of them unless told otherwise,
    // because "which outputs already exist" does not depend on which ones the person converts.
    private static readonly OutputTarget[] AllTargets = Enum.GetValues<OutputTarget>();

    /// <summary>Absolute path to the folder to scan.</summary>
    public required string RootFolder { get; init; }

    /// <summary>True to include sub-directories; false for the top folder only.</summary>
    public bool Recursive { get; init; }

    /// <summary>
    /// Which output types to report the status of. Defaults to every <see cref="OutputTarget"/>
    /// (PDF, ODG and SLA), so a scan always shows what exists. Order here becomes column order.
    /// </summary>
    public IReadOnlyList<OutputTarget> EnabledTargets { get; init; } = AllTargets;

    /// <summary>Same-folder or separate-folder output location (CLAUDE.md section 8).</summary>
    public OutputLocationMode LocationMode { get; init; } = OutputLocationMode.SameFolder;

    /// <summary>
    /// Root of the separate output folder. Required when <see cref="LocationMode"/> is
    /// <see cref="OutputLocationMode.SeparateFolder"/>; ignored otherwise.
    /// </summary>
    public string? SeparateOutputRoot { get; init; }
}
