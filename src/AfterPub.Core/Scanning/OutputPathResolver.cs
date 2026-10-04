namespace AfterPub.Core.Scanning;

/// <summary>
/// Default <see cref="IOutputPathResolver"/>. Same-folder mode writes next to the
/// source; separate-folder mode mirrors the source's folder structure underneath
/// <see cref="ScanOptions.SeparateOutputRoot"/> so files with the same base name in
/// different source folders never collide (CLAUDE.md section 3.3).
/// </summary>
public sealed class OutputPathResolver : IOutputPathResolver
{
    public string ResolveOutputPath(SourceFile source, OutputTarget target, ScanOptions options)
    {
        string extension = GetExtension(target);
        string fileName = source.BaseName + extension;

        if (options.LocationMode == OutputLocationMode.SameFolder)
        {
            return Path.Combine(source.SourceFolder, fileName);
        }

        if (string.IsNullOrWhiteSpace(options.SeparateOutputRoot))
        {
            throw new InvalidOperationException(
                "ScanOptions.SeparateOutputRoot is required when LocationMode is SeparateFolder.");
        }

        // RelativePath includes the file name, e.g. "Flyers\Summer\Flyer.pub".
        // Take just the directory part so it can be recombined with the new file name.
        string relativeFolder = Path.GetDirectoryName(source.RelativePath) ?? string.Empty;
        string mirroredFolder = Path.Combine(options.SeparateOutputRoot, relativeFolder);
        return Path.Combine(mirroredFolder, fileName);
    }

    private static string GetExtension(OutputTarget target)
    {
        switch (target)
        {
            case OutputTarget.Pdf:
                return ".pdf";
            case OutputTarget.Odg:
                return ".odg";
            case OutputTarget.Sla:
                return ".sla";
            default:
                throw new ArgumentOutOfRangeException(nameof(target), target, "Unknown output target.");
        }
    }
}
