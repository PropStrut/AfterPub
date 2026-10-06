namespace AfterPub.Core.Scanning;

/// <summary>
/// One row of the main list: a source .pub file plus the status of each enabled
/// output type for it. See CLAUDE.md section 3.3 for what "converted" means.
/// </summary>
public sealed class ConversionRow
{
    /// <summary>The source .pub file this row describes.</summary>
    public SourceFile Source { get; }

    /// <summary>Status for each output type that is currently enabled in settings.</summary>
    public IReadOnlyList<OutputInfo> Outputs { get; }

    /// <summary>
    /// True only when every enabled output target exists for this source
    /// (CLAUDE.md section 3.3: "converted" is tracked per output type, and a row is
    /// fully converted only when all enabled targets are present).
    /// </summary>
    public bool IsFullyConverted => this.Outputs.All(output => output.Exists);

    /// <summary>
    /// How much newer the source may be than an output before the output counts as out of date.
    /// File systems store times with different precision (FAT keeps two-second steps), and copies
    /// can shift a time slightly, so a small difference is not treated as a change.
    /// </summary>
    public static readonly TimeSpan OutOfDateTolerance = TimeSpan.FromSeconds(2);

    public ConversionRow(SourceFile source, IReadOnlyList<OutputInfo> outputs)
    {
        this.Source = source;
        this.Outputs = outputs;
    }

    /// <summary>
    /// True when the output for <paramref name="target"/> exists but is older than the source, so
    /// the .pub was probably edited after the output was made. Compares file dates only; copying
    /// or cloud sync can move dates, so this is a hint to look, not proof (CLAUDE.md section 3.3).
    /// A missing output is not "out of date": it is missing.
    /// </summary>
    public bool IsOutOfDate(OutputTarget target)
    {
        OutputInfo? output = this.GetOutput(target);
        if (output is not { Exists: true } || output.LastWriteTimeUtc is null)
        {
            return false;
        }

        return this.Source.LastWriteTimeUtc - output.LastWriteTimeUtc.Value > OutOfDateTolerance;
    }

    /// <summary>True when any output this row reports on is out of date.</summary>
    public bool HasOutOfDateOutput => this.Outputs.Any(output => this.IsOutOfDate(output.Target));

    /// <summary>Gets the OutputInfo for a specific target, or null if that target was not requested for this row.</summary>
    public OutputInfo? GetOutput(OutputTarget target)
    {
        return this.Outputs.FirstOrDefault(output => output.Target == target);
    }
}
