namespace AfterPub.Core.Scanning;

/// <summary>
/// Works out where the output file for a given source and target should live,
/// under the current <see cref="ScanOptions"/>.
/// </summary>
public interface IOutputPathResolver
{
    /// <summary>Returns the expected output path for <paramref name="source"/> and <paramref name="target"/>.</summary>
    string ResolveOutputPath(SourceFile source, OutputTarget target, ScanOptions options);
}
