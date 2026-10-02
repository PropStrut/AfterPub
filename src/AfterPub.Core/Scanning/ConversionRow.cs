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

    public ConversionRow(SourceFile source, IReadOnlyList<OutputInfo> outputs)
    {
        this.Source = source;
        this.Outputs = outputs;
    }

    /// <summary>Gets the OutputInfo for a specific target, or null if that target was not requested for this row.</summary>
    public OutputInfo? GetOutput(OutputTarget target)
    {
        return this.Outputs.FirstOrDefault(output => output.Target == target);
    }
}
