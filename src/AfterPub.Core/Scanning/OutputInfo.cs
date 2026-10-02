namespace AfterPub.Core.Scanning;

/// <summary>
/// The state of one output type (e.g. PDF) for one source file, as read live from
/// the file system. Does not use the per-folder record file (CLAUDE.md section 7) —
/// that is layered on in a later phase to add staleness detection, engine used,
/// score, and missing fonts.
/// </summary>
public sealed class OutputInfo
{
    /// <summary>Which output this describes.</summary>
    public OutputTarget Target { get; }

    /// <summary>The path this output would have (or does have), given current settings.</summary>
    public string ExpectedPath { get; }

    /// <summary>Whether a file currently exists at <see cref="ExpectedPath"/>.</summary>
    public bool Exists { get; }

    /// <summary>Size of the output file in bytes, if it exists.</summary>
    public long? SizeBytes { get; }

    /// <summary>Last write time of the output file (UTC), if it exists.</summary>
    public DateTime? LastWriteTimeUtc { get; }

    private OutputInfo(OutputTarget target, string expectedPath, bool exists, long? sizeBytes, DateTime? lastWriteTimeUtc)
    {
        this.Target = target;
        this.ExpectedPath = expectedPath;
        this.Exists = exists;
        this.SizeBytes = sizeBytes;
        this.LastWriteTimeUtc = lastWriteTimeUtc;
    }

    /// <summary>Builds an OutputInfo by checking the file system at <paramref name="expectedPath"/> right now.</summary>
    public static OutputInfo FromFileSystem(OutputTarget target, string expectedPath)
    {
        FileInfo fileInfo = new FileInfo(expectedPath);
        if (fileInfo.Exists)
        {
            return new OutputInfo(target, expectedPath, exists: true, fileInfo.Length, fileInfo.LastWriteTimeUtc);
        }

        return new OutputInfo(target, expectedPath, exists: false, sizeBytes: null, lastWriteTimeUtc: null);
    }
}
