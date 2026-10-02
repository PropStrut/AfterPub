namespace AfterPub.Core.Scanning;

/// <summary>
/// Default <see cref="IPubFileScanner"/>. Walks the file system for .pub files and
/// combines each one with an <see cref="IOutputPathResolver"/> to report live status.
/// </summary>
public sealed class PubFileScanner : IPubFileScanner
{
    private readonly IOutputPathResolver _outputPathResolver;

    public PubFileScanner(IOutputPathResolver outputPathResolver)
    {
        this._outputPathResolver = outputPathResolver;
    }

    public IReadOnlyList<ConversionRow> Scan(ScanOptions options)
    {
        if (!Directory.Exists(options.RootFolder))
        {
            throw new DirectoryNotFoundException($"Scan folder not found: {options.RootFolder}");
        }

        // The SearchOption overload of EnumerateFiles aborts the whole scan on the first
        // inaccessible folder (for example "System Volume Information" at a drive root).
        // EnumerationOptions lets us skip such folders and keep going (CLAUDE.md section 3.4:
        // one bad item never aborts the batch). Skipping the System attribute also avoids
        // "$RECYCLE.BIN" and similar protected folders.
        EnumerationOptions enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = options.Recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System
        };

        List<ConversionRow> rows = new List<ConversionRow>();

        foreach (string fullPath in Directory.EnumerateFiles(options.RootFolder, "*.pub", enumerationOptions))
        {
            // SSH public keys also end in .pub but are not Publisher documents.
            if (SshKeyDetector.IsSshPublicKey(fullPath))
            {
                continue;
            }

            FileInfo fileInfo = new FileInfo(fullPath);
            string relativePath = Path.GetRelativePath(options.RootFolder, fullPath);
            SourceFile source = new SourceFile(fullPath, relativePath, fileInfo.Length, fileInfo.LastWriteTimeUtc);

            List<OutputInfo> outputs = new List<OutputInfo>();
            foreach (OutputTarget target in options.EnabledTargets)
            {
                string expectedPath = this._outputPathResolver.ResolveOutputPath(source, target, options);
                outputs.Add(OutputInfo.FromFileSystem(target, expectedPath));
            }

            rows.Add(new ConversionRow(source, outputs));
        }

        return rows;
    }
}
